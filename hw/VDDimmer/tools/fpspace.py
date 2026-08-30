"""Find genuinely clear board space for a small footprint.

Supersedes an ad-hoc check that built a track list and then never tested against it,
reporting 4648 "clear" 0805 positions in a corridor packed with the GATE_IN fan-out.
Obstacles here are pads, vias, **tracks**, and silkscreen -- all four, on F.Cu.

Usage: fpspace.py CX CY RADIUS [W] [H]
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
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
                for t in tracks:
                    # distance from the track centreline to the footprint rectangle
                    rect = {"x0": x0, "y0": y0, "x1": x1, "y1": y1}
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


if __name__ == "__main__":
    cx, cy, r = float(sys.argv[1]), float(sys.argv[2]), float(sys.argv[3])
    w = float(sys.argv[4]) if len(sys.argv) > 4 else 2.8
    h = float(sys.argv[5]) if len(sys.argv) > 5 else 1.8
    ign = tuple(sys.argv[6].split(",")) if len(sys.argv) > 6 else ()
    s = clear_spots(cx, cy, r, w, h, ignore_refs=ign)
    print("clear %.1f x %.1f footprint sites within %.0f mm of (%.2f, %.2f): %d"
          % (w, h, r, cx, cy, len(s)))
    for d, x, y in s[:20]:
        print("   %5.2f mm  (%.2f, %.2f)" % (d, x, y))
