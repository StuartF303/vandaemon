"""Propose GND return vias beside layer transitions that have none.

Only worth running once GND pours exist on BOTH layers -- a stitching via between one
reference plane and empty space does nothing. Each candidate must:
  * sit within RADIUS of the signal via it serves
  * clear every foreign pad / track / via by CLR
  * land inside the GND fill on F.Cu *and* B.Cu, so it is actually connected

Prints a table plus ready-to-paste add_via arguments.
"""
import importlib.util, math, os, sys

SP = os.path.dirname(os.path.abspath(__file__))
def _l(n):
    s = importlib.util.spec_from_file_location(n, SP + f"/{n}.py")
    m = importlib.util.module_from_spec(s); s.loader.exec_module(m); return m
g   = _l("geom")
px  = _l("polyidx")
con = _l("connect")

CLR, VIA, RADIUS = 0.2, 0.8, 1.2

pads, tracks, vias = g.load()
zs = con.zones()
gnd_idx = {}
for lay in ("F.Cu", "B.Cu"):
    gnd_idx[lay] = [px.PolyIndex(z[3]) for z in zs if z[0] == "GND" and z[1] == lay]
if not gnd_idx["F.Cu"] or not gnd_idx["B.Cu"]:
    print("GND pour missing on a layer -- return vias would be inert. Stopping.")
    sys.exit(1)

def in_gnd(lay, x, y):
    ring = px.disc_samples(x, y, VIA/2)
    return all(any(i.point_in(*p) for i in gnd_idx[lay]) for p in ring)

def clear(x, y):
    r = VIA/2
    for t in tracks:
        if t["net"] == "GND": continue
        if g.seg_dist(x, y, t["x1"], t["y1"], t["x2"], t["y2"]) - r - t["w"]/2 < CLR:
            return False
    for p in pads:
        if p["net"] == "GND": continue
        dx = max(p["x0"]-x, 0, x-p["x1"]); dy = max(p["y0"]-y, 0, y-p["y1"])
        if math.hypot(dx, dy) - r < CLR: return False
    for v in vias:
        if math.hypot(x-v["x"], y-v["y"]) - r - v["d"]/2 < CLR: return False
    return True

gv  = [v for v in vias if v["net"] == "GND"]
gp  = [p for p in pads if p["net"] == "GND" and "*.Cu" in p["layers"]]
sig = [v for v in vias if v["net"] != "GND"]

def nearest_gnd(x, y):
    return min([math.hypot(x-o["x"], y-o["y"]) for o in gv] +
               [math.hypot(x-p["cx"], y-p["cy"]) for p in gp])

need = [v for v in sig if nearest_gnd(v["x"], v["y"]) > 1.0]
print(f"{len(need)} of {len(sig)} layer transitions have no GND within 1.0 mm\n")

placed, failed = [], []
for v in sorted(need, key=lambda v: -nearest_gnd(v["x"], v["y"])):
    best = None
    for rad in [x/100 for x in range(85, int(RADIUS*100)+1, 5)]:
        for k in range(72):
            a = 2*math.pi*k/72
            x = round(v["x"] + rad*math.cos(a), 3)
            y = round(v["y"] + rad*math.sin(a), 3)
            if not clear(x, y): continue
            if not (in_gnd("F.Cu", x, y) and in_gnd("B.Cu", x, y)): continue
            best = (x, y, rad); break
        if best: break
    if best:
        placed.append((v, best))
        vias.append(dict(x=best[0], y=best[1], d=VIA, net="GND"))   # occupy the space
        gv.append(vias[-1])
    else:
        failed.append(v)

print(f"placed {len(placed)}, no room for {len(failed)}\n")
for v, (x, y, rad) in placed:
    # RADIUS is deliberately wider than the 1.0 mm criterion -- a via at 1.05 mm still
    # gives a return path, it just does not cross the arbitrary line emc.py counts.
    flag = "" if rad <= 1.0 else "   (> 1.0 mm, will still count as 'missing' in emc.py)"
    print(f"   GND via ({x:8.3f}, {y:8.3f})  {rad:.2f} mm from {v['net']:26s} ({v['x']},{v['y']}){flag}")
if failed:
    print("\nno room:")
    for v in failed:
        print(f"   {v['net']:26s} ({v['x']},{v['y']})")
print("\n--- add_via calls ---")
for v, (x, y, rad) in placed:
    print(f'add_via net_name="GND" x={x} y={y} pad_size={VIA} drill=0.4')
