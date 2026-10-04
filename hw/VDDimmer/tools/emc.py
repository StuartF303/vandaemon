"""EMC / return-path measurements for VANDIMMER-4CH+2A.

Re-take after every zone refill -- all three numbers move when copper moves.

  1. B.Cu GND pour: island count and area (refutes/confirms kicad-happy PS-002)
  2. F.Cu routed nets crossing a gap in the B.Cu GND pour (longest unbacked run)
  3. Layer transitions (vias) with no GND return via within 1.0 mm
  4. B.Cu GND coverage under the buck footprint, +5 mm margin
"""
import importlib.util, math, os, re, sys

SP = os.path.dirname(os.path.abspath(__file__))
def _load(name):
    s = importlib.util.spec_from_file_location(name, SP + f"/{name}.py")
    m = importlib.util.module_from_spec(s); s.loader.exec_module(m); return m
g   = _load("geom")
px  = _load("polyidx")
con = _load("connect")

pads, tracks, vias = g.load()
zs = con.zones()
gnd_b = [z for z in zs if z[0] == "GND" and z[1] == "B.Cu"]

print("=== 1. B.Cu GND pour ===")
tot = sum(con.poly_area(z[3]) for z in gnd_b)
print(f"   {len(gnd_b)} filled island(s), {tot:.1f} mm^2 total")
for z in sorted(gnd_b, key=lambda z: -con.poly_area(z[3]))[:5]:
    xs = [p[0] for p in z[3]]; ys = [p[1] for p in z[3]]
    print(f"     {con.poly_area(z[3]):9.1f} mm^2  bbox x {min(xs):.1f}-{max(xs):.1f} "
          f"y {min(ys):.1f}-{max(ys):.1f}  ({len(z[3])} vertices)")

idx = [px.PolyIndex(z[3]) for z in gnd_b]
def backed(x, y):
    return any(i.point_in(x, y) for i in idx)

print("\n=== 2. F.Cu nets crossing a gap in the B.Cu GND pour ===")
STEP = 0.1
worst = {}
for t in tracks:
    if t["layer"] != "F.Cu" or t["net"] in ("GND", ""): continue
    pts = px.seg_samples(t["x1"], t["y1"], t["x2"], t["y2"], STEP)
    run = 0.0; best = 0.0
    for x, y in pts:
        if backed(x, y): run = 0.0
        else:
            run += STEP
            best = max(best, run)
    if best > 0:
        n = t["net"]
        if best > worst.get(n, (0,))[0]:
            worst[n] = (best, f'({t["x1"]},{t["y1"]})-({t["x2"]},{t["y2"]})')
fcu_nets = {t["net"] for t in tracks if t["layer"] == "F.Cu" and t["net"] not in ("GND", "")}
print(f"   {len(worst)} of {len(fcu_nets)} routed F.Cu nets cross an unbacked gap")
for n, (L, loc) in sorted(worst.items(), key=lambda kv: -kv[1][0])[:12]:
    print(f"     {L:5.2f} mm  {n:28s} {loc}")

print("\n=== 3. Layer transitions without a GND return via ===")
gvias = [v for v in vias if v["net"] == "GND"]
gpads = [p for p in pads if p["net"] == "GND" and "*.Cu" in p["layers"]]
sig = [v for v in vias if v["net"] != "GND"]
rows = []
for v in sig:
    d = min([math.hypot(v["x"]-o["x"], v["y"]-o["y"]) for o in gvias] +
            [math.hypot(v["x"]-p["cx"], v["y"]-p["cy"]) for p in gpads] or [9e9])
    rows.append((d, v))
rows.sort(key=lambda r: -r[0])
bad = [r for r in rows if r[0] > 1.0]
print(f"   {len(sig)} non-GND vias; {len(bad)} have no GND via/PTH within 1.0 mm")
for d, v in bad[:12]:
    print(f"     {d:5.2f} mm  {v['net']:28s} ({v['x']},{v['y']})")

print("\n=== 4. B.Cu GND under the buck (U2 footprint + 5 mm) ===")
u2 = [p for p in pads if p["ref"] == "U2"]
if u2:
    x0 = min(p["x0"] for p in u2) - 5; x1 = max(p["x1"] for p in u2) + 5
    y0 = min(p["y0"] for p in u2) - 5; y1 = max(p["y1"] for p in u2) + 5
    N = 80
    hit = sum(1 for i in range(N) for j in range(N)
              if backed(x0 + (x1-x0)*i/(N-1), y0 + (y1-y0)*j/(N-1)))
    print(f"   window x {x0:.2f}-{x1:.2f}  y {y0:.2f}-{y1:.2f}")
    print(f"   GND pour covers {hit}/{N*N} = {100*hit/(N*N):.1f}% of sample points")
    foreign = [t for t in tracks if t["layer"] == "B.Cu" and t["net"] != "GND"
               and not (max(t["x1"],t["x2"]) < x0 or min(t["x1"],t["x2"]) > x1 or
                        max(t["y1"],t["y2"]) < y0 or min(t["y1"],t["y2"]) > y1)]
    fvias = [v for v in vias if v["net"] != "GND" and x0 <= v["x"] <= x1 and y0 <= v["y"] <= y1]
    print(f"   foreign B.Cu tracks in window: {len(foreign)}   foreign vias: {len(fvias)}")
    for v in fvias: print(f"     via {v['net']} ({v['x']},{v['y']})")
