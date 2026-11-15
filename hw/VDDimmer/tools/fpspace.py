"""Find genuinely clear board space for a small footprint, and check courtyards.

Supersedes an ad-hoc check that built a track list and then never tested against it,
reporting 4648 "clear" 0805 positions in a corridor packed with the GATE_IN fan-out.
Obstacles here are pads, vias, **tracks**, and silkscreen -- all four.

`courtyard_conflicts` exists because pad clearance is not enough. Swapping L1/L3 to a
wider curated land put every new pad clear of every foreign net, and still produced two
DRC *errors*: the courtyard wings overlapped C25/C26 by 0.25 mm. Check both.

Usage:
    fpspace.py CX CY RADIUS [W] [H] [IGNORE_REFS]
    fpspace.py --courtyards [REF ...]
"""
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BOARD = os.path.join(os.path.dirname(HERE), "VANDIMMER-4CH2A.kicad_pcb")
sys.path.insert(0, HERE)
import geom      # noqa: E402
import silkspace  # noqa: E402

CLEARANCE = 0.2   # min_clearance from the project rules


def clear_spots(cx, cy, radius, w=2.8, h=1.8, step=0.25, layer="F.Cu",
                ignore_refs=()):
    pads, tracks, vias = geom.load()
    pads = [p for p in pads if p["ref"] not in ignore_refs]
    boxes = silkspace.silk_boxes()

    win = radius + max(w, h) + 2

    def inwin(x, y):
        return abs(x - cx) < win and abs(y - cy) < win

    pads = [p for p in pads if inwin(p["cx"], p["cy"])]
    vias = [v for v in vias if inwin(v["x"], v["y"])]
    tracks = [t for t in tracks
              if t["layer"] == layer and (inwin(t["x1"], t["y1"]) or inwin(t["x2"], t["y2"]))]
    boxes = [b for b in boxes if inwin((b[0] + b[2]) / 2, (b[1] + b[3]) / 2)]

    hw, hh = w / 2 + CLEARANCE, h / 2 + CLEARANCE
    out = []
    n = int(radius / step)
    for i in range(-n, n + 1):
        for j in range(-n, n + 1):
            x, y = cx + i * step, cy + j * step
            d = math.hypot(x - cx, y - cy)
            if d > radius:
                continue
            x0, y0, x1, y1 = x - hw, y - hh, x + hw, y + hh
            ok = True
            for p in pads:
                if not (p["x1"] < x0 or p["x0"] > x1 or p["y1"] < y0 or p["y0"] > y1):
                    ok = False
                    break
            if ok:
                for v in vias:
                    r = v["d"] / 2 + CLEARANCE
                    if abs(v["x"] - x) < hw + r and abs(v["y"] - y) < hh + r:
                        ok = False
                        break
            if ok:
                rect = {"x0": x0, "y0": y0, "x1": x1, "y1": y1}
                for t in tracks:
                    if geom.seg_rect_dist(t["x1"], t["y1"], t["x2"], t["y2"],
                                          rect) < t["w"] / 2:
                        ok = False
                        break
            if ok:
                for b in boxes:
                    if not (b[2] < x0 or b[0] > x1 or b[3] < y0 or b[1] > y1):
                        ok = False
                        break
            if ok:
                out.append((round(d, 2), x, y))
    out.sort()
    return out


def courtyards(path=BOARD):
    """{ref: (x0, y0, x1, y1)} bounding box of each footprint's F.CrtYd, board coords.

    Bounding boxes rather than the true polygon, so a notched courtyard reads slightly
    larger than KiCad treats it. Deliberate: this is a pre-flight check and
    over-reporting is the safe direction.
    """
    src = open(path, encoding="utf-8").read()
    out = {}
    for a, b in geom.blocks(src, "\t(footprint "):
        blk = src[a:b]
        ref = re.search(r'\(property "Reference" "([^"]+)"', blk)
        at = re.search(r"\n\t\t\(at ([-\d.]+) ([-\d.]+)(?: ([-\d.]+))?\)", blk)
        if not ref or not at:
            continue
        fx, fy = float(at.group(1)), float(at.group(2))
        ang = math.radians(-float(at.group(3) or 0))
        ca, sa = math.cos(ang), math.sin(ang)
        pts = []
        for m in re.finditer(r"\(fp_(?:line|rect|poly|circle)\b(.*?)\n\t\t\)", blk, re.S):
            g = m.group(0)
            if "F.CrtYd" not in g:
                continue
            for mm in re.finditer(r"\((?:start|end|center|xy) ([-\d.]+) ([-\d.]+)\)", g):
                px, py = float(mm.group(1)), float(mm.group(2))
                pts.append((fx + px * ca - py * sa, fy + px * sa + py * ca))
        if pts:
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            out[ref.group(1)] = (min(xs), min(ys), max(xs), max(ys))
    return out


def courtyard_conflicts(only=None, path=BOARD):
    """Overlapping courtyard pairs, worst first. `only` limits one side to those refs.

    Run after ANY footprint swap or move: a courtyard overlap is a DRC error, and it is
    not predicted by checking pad clearance.
    """
    boxes = courtyards(path)
    refs = sorted(boxes)
    hits = []
    for i, a in enumerate(refs):
        for b in refs[i + 1:]:
            if only and a not in only and b not in only:
                continue
            ax0, ay0, ax1, ay1 = boxes[a]
            bx0, by0, bx1, by1 = boxes[b]
            ox = min(ax1, bx1) - max(ax0, bx0)
            oy = min(ay1, by1) - max(ay0, by0)
            if ox > 0 and oy > 0:
                hits.append((a, b, ox, oy))
    return sorted(hits, key=lambda h: -(h[2] * h[3]))


if __name__ == "__main__":
    if sys.argv[1:2] == ["--courtyards"]:
        bad = courtyard_conflicts(set(sys.argv[2:]) or None)
        print("courtyard overlaps: %d" % len(bad))
        for a, b, ox, oy in bad:
            print("   %-6s <-> %-6s  overlap %.3f x %.3f mm" % (a, b, ox, oy))
        sys.exit(1 if bad else 0)

    cx, cy, r = float(sys.argv[1]), float(sys.argv[2]), float(sys.argv[3])
    w = float(sys.argv[4]) if len(sys.argv) > 4 else 2.8
    h = float(sys.argv[5]) if len(sys.argv) > 5 else 1.8
    ign = tuple(sys.argv[6].split(",")) if len(sys.argv) > 6 else ()
    s = clear_spots(cx, cy, r, w, h, ignore_refs=ign)
    print("clear %.1f x %.1f footprint sites within %.0f mm of (%.2f, %.2f): %d"
          % (w, h, r, cx, cy, len(s)))
    for d, x, y in s[:20]:
        print("   %5.2f mm  (%.2f, %.2f)" % (d, x, y))
