"""Net connectivity: group a net's copper (tracks, vias, pads, zone islands) into
electrically connected components. Answers "is this net actually one piece?" --
which the DRC ratsnest only hints at.

    python connect.py +3V3 +5V GND
"""
import importlib.util, math, os, re, sys

SP = os.path.dirname(os.path.abspath(__file__))
_s = importlib.util.spec_from_file_location("geom", SP + "/geom.py")
g = importlib.util.module_from_spec(_s); _s.loader.exec_module(g)
_p = importlib.util.spec_from_file_location("polyidx", SP + "/polyidx.py")
px = importlib.util.module_from_spec(_p); _p.loader.exec_module(px)

BOARD = g.BOARD

def zones(path=BOARD):
    """[(net, layer, priority, [(x,y), ...]), ...] -- one entry per filled island."""
    src = open(path, encoding="utf-8").read()
    out = []
    for zs, ze in g.blocks(src, "\n\t(zone"):
        zb = src[zs:ze]
        m = re.search(r'\(net_name "([^"]*)"\)', zb) or re.search(r'\(net "([^"]*)"\)', zb)
        net = m.group(1) if m else ""
        lay = re.search(r'\(layer "([^"]+)"\)', zb)
        lay = lay.group(1) if lay else ""
        pri = re.search(r'\(priority (\d+)\)', zb)
        pri = int(pri.group(1)) if pri else 0
        for fs, fe in g.blocks(zb, "\n\t\t(filled_polygon"):
            fb = zb[fs:fe]
            fl = re.search(r'\(layer "([^"]+)"\)', fb)
            pts = [(float(a), float(b)) for a, b in
                   re.findall(r'\(xy ([-\d.]+) ([-\d.]+)\)', fb)]
            if len(pts) >= 3:
                out.append((net, fl.group(1) if fl else lay, pri, pts))
    return out

def poly_area(pts):
    n = len(pts)
    return abs(sum(pts[i][0]*pts[(i+1) % n][1] - pts[(i+1) % n][0]*pts[i][1]
                   for i in range(n))) / 2

def inside(pts, x, y):
    c = False; n = len(pts); j = n-1
    for i in range(n):
        xi, yi = pts[i]; xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj-xi)*(y-yi)/(yj-yi+1e-15)+xi:
            c = not c
        j = i
    return c

def pad_layers(p):
    s = p["layers"]
    return {"F.Cu"} if '"F.Cu"' in s and "*.Cu" not in s else \
           {"B.Cu"} if '"B.Cu"' in s and "*.Cu" not in s else {"F.Cu", "B.Cu"}

# Copper must actually OVERLAP to conduct. A tangent touch -- gap exactly equal to the
# radius, or a track edge exactly on a pad edge -- is what KiCad calls unconnected, and it
# is how a via lands 0.0000 mm onto an ESP32 pad and leaves the part unpowered. Require a
# real overlap, and report anything thinner than MARGINAL as fragile.
OVERLAP = 1e-4
MARGINAL = 0.05
marginal = []

class UF:
    def __init__(s, n): s.p = list(range(n))
    def find(s, a):
        while s.p[a] != a: s.p[a] = s.p[s.p[a]]; a = s.p[a]
        return a
    def union(s, a, b):
        ra, rb = s.find(a), s.find(b)
        if ra != rb: s.p[ra] = rb

def lbl(k, o):
    if k == "pad":  return f'pad {o["ref"]}.{o["num"]}'
    if k == "via":  return f'via ({o["x"]:.3f},{o["y"]:.3f})'
    if k == "zone": return f'zone {o[1]} prio{o[2]} {poly_area(o[3]):.1f}mm2'
    L = math.hypot(o["x2"]-o["x1"], o["y2"]-o["y1"])
    return f'trk {o["layer"]} {L:.2f}mm ({o["x1"]},{o["y1"]})'

def analyse(net, pads, tracks, vias, zs, verbose=True):
    T = [t for t in tracks if t["net"] == net]
    V = [v for v in vias if v["net"] == net]
    P = [p for p in pads if p["net"] == net]
    Z = [z for z in zs if z[0] == net]
    nodes = ([("track", t) for t in T] + [("via", v) for v in V] +
             [("pad", p) for p in P] + [("zone", z) for z in Z])
    uf = UF(len(nodes))

    def layers(kind, o):
        if kind == "track": return {o["layer"]}
        if kind == "via":   return {"F.Cu", "B.Cu"}
        if kind == "pad":   return pad_layers(o)
        return {o[1]}

    def label(k, o):
        if k == "pad":   return f'pad {o["ref"]}.{o["num"]}'
        if k == "via":   return f'via ({o["x"]:.3f},{o["y"]:.3f})'
        if k == "zone":  return f'zone {o[1]} prio{o[2]} {poly_area(o[3]):.1f}mm2'
        L = math.hypot(o["x2"]-o["x1"], o["y2"]-o["y1"])
        return f'trk {o["layer"]} {L:.2f}mm ({o["x1"]},{o["y1"]})-({o["x2"]},{o["y2"]})'

    idx = {}
    def zidx(z):
        k = id(z)
        if k not in idx: idx[k] = px.PolyIndex(z[3])
        return idx[k]

    def samples(kind, o):
        if kind == "via":   return px.disc_samples(o["x"], o["y"], o["d"] / 2)
        if kind == "pad":   return px.rect_samples(o["x0"], o["y0"], o["x1"], o["y1"])
        return px.seg_samples(o["x1"], o["y1"], o["x2"], o["y2"])

    def touches(ka, a, kb, b):
        if not (layers(ka, a) & layers(kb, b)): return False
        if ka == "zone" and kb == "zone":
            if a[1] != b[1]: return False
            ia, ib = zidx(a), zidx(b)
            return ia.any_in(b[3]) or ib.any_in(a[3])
        if ka == "zone" or kb == "zone":
            z, kk, o = (a, kb, b) if ka == "zone" else (b, ka, a)
            return zidx(z).any_in(samples(kk, o))
        ov = overlap(ka, a, kb, b)
        if ov is None: return False
        if OVERLAP <= ov < MARGINAL:
            marginal.append((ov, label(ka, a), label(kb, b)))
        return ov >= OVERLAP

    def overlap(ka, a, kb, b):
        """How deeply the two pieces of copper overlap, in mm. None if not applicable."""
        if ka == "track" and kb == "track":
            return (a["w"] + b["w"]) / 2 - g.seg_seg_dist(
                (a["x1"], a["y1"]), (a["x2"], a["y2"]), (b["x1"], b["y1"]), (b["x2"], b["y2"]))
        if ka == "track" and kb == "pad":
            return a["w"]/2 - g.seg_rect_dist(a["x1"], a["y1"], a["x2"], a["y2"], b)
        if ka == "pad" and kb == "track":
            return overlap(kb, b, ka, a)
        if ka == "track" and kb == "via":
            return a["w"]/2 + b["d"]/2 - g.seg_dist(b["x"], b["y"],
                                                    a["x1"], a["y1"], a["x2"], a["y2"])
        if ka == "via" and kb == "track":
            return overlap(kb, b, ka, a)
        if ka == "via" and kb == "via":
            return (a["d"] + b["d"]) / 2 - math.hypot(a["x"]-b["x"], a["y"]-b["y"])
        if ka == "via" and kb == "pad":
            dx = max(b["x0"]-a["x"], 0, a["x"]-b["x1"])
            dy = max(b["y0"]-a["y"], 0, a["y"]-b["y1"])
            return a["d"]/2 - math.hypot(dx, dy)
        if ka == "pad" and kb == "via":
            return overlap(kb, b, ka, a)
        if ka == "pad" and kb == "pad":
            return min(min(a["x1"], b["x1"]) - max(a["x0"], b["x0"]),
                       min(a["y1"], b["y1"]) - max(a["y0"], b["y0"]))
        return None

    for i in range(len(nodes)):
        for j in range(i+1, len(nodes)):
            if touches(nodes[i][0], nodes[i][1], nodes[j][0], nodes[j][1]):
                uf.union(i, j)

    groups = {}
    for i, (k, o) in enumerate(nodes):
        groups.setdefault(uf.find(i), []).append((k, o))

    order = sorted(groups.values(), key=len, reverse=True)
    if verbose:
        print(f"=== {net}: {len(nodes)} items -> {len(order)} connected group(s) ===")
        for gi, mem in enumerate(order):
            pads_in = sorted({f'{o["ref"]}.{o["num"]}' for k, o in mem if k == "pad"})
            print(f"  group {gi}: {len(mem):4d} items, {len(pads_in)} pad(s)")
            if gi == 0 and len(order) > 1:
                print(f"      pads: {', '.join(pads_in[:24])}"
                      f"{' ...' if len(pads_in) > 24 else ''}")
            elif gi > 0:
                for k, o in mem: print(f"      {label(k, o)}")
    return order

if __name__ == "__main__":
    pads, tracks, vias = g.load()
    zs = zones()
    if sys.argv[1:]:
        nets = sys.argv[1:]
        for n in nets:
            analyse(n, pads, tracks, vias, zs)
            print()
    else:
        nets = sorted({o["net"] for o in tracks} | {v["net"] for v in vias} |
                      {p["net"] for p in pads} | {z[0] for z in zs})
        nets = [n for n in nets if n and not n.startswith("unconnected-")]
        split = []
        for n in nets:
            order = analyse(n, pads, tracks, vias, zs, verbose=False)
            if len(order) > 1: split.append((n, order))
        print(f"=== {len(nets)} nets checked, {len(split)} split into more than one group ===")
        print()
        for n, order in split:
            print(f"--- {n}: {len(order)} groups")
            for gi, mem in enumerate(order):
                ps = sorted({f'{o["ref"]}.{o["num"]}' for k, o in mem if k == "pad"})
                extra = "" if gi == 0 else "   " + ", ".join(
                    lbl(k, o) for k, o in mem)[:160]
                print(f"    g{gi}: {len(mem):3d} items  pads: {', '.join(ps[:12]) or '(none)'}{extra}")
            print()
    if marginal:
        print(f"=== {len(marginal)} MARGINAL contacts (overlap < {MARGINAL} mm) ===")
        print("    copper this thin is one rounding away from an open circuit")
        for ov, a, b in sorted(marginal)[:30]:
            print(f"   {ov*1000:6.1f} um  {a}  <->  {b}")
