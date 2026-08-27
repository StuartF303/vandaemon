"""Validate proposed routes offline, then emit them.

Usage: routes are a list of dicts:
   {"net": "...", "w": 0.25, "pts": [(x,y), ...], "layer": "F.Cu"}
check(routes) reports every clearance violation against existing pads, tracks,
vias, the board edge, and the other proposed routes.
"""
import os, importlib.util, math, sys, json
SP = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("geom", SP + "/geom.py")
g = importlib.util.module_from_spec(spec); spec.loader.exec_module(g)

CL = 0.2
EDGE = 0.5
BOARD_BOX = (100.0, 60.0, 200.0, 140.0)

def segs(r):
    p = r["pts"]
    return [(p[i][0], p[i][1], p[i+1][0], p[i+1][1]) for i in range(len(p)-1)]

def check(routes, extra_clear=0.0, verbose=True):
    pads, tracks, vias = g.load()
    bad = []
    proposed = []
    for ri, r in enumerate(routes):
        for s in segs(r):
            proposed.append((ri, r["net"], r.get("w", 0.25), r.get("layer", "F.Cu"), s))
    for ri, net, w, layer, s in proposed:
        x1, y1, x2, y2 = s
        half = w / 2
        # board edge
        bx0, by0, bx1, by1 = BOARD_BOX
        m = min(x1 - bx0, x2 - bx0, y1 - by0, y2 - by0,
                bx1 - x1, bx1 - x2, by1 - y1, by1 - y2)
        if m - half < EDGE:
            bad.append(f"[{net}] seg {s} only {m-half:.3f} mm from board edge (need {EDGE})")
        for p in pads:
            if p["net"] == net: continue
            # only pads that actually have copper on this trace's layer matter
            if "*.Cu" not in p["layers"] and layer not in p["layers"]:
                continue
            d = g.seg_rect_dist(x1, y1, x2, y2, p)
            if d - half < CL - 1e-6 + extra_clear:
                bad.append(f"[{net}] seg ({x1},{y1})-({x2},{y2}) vs pad {p['ref']}.{p['num']} "
                           f"[{p['net']}]: gap {d-half:.3f} mm")
        for t in tracks:
            if t["net"] == net or t["layer"] != layer: continue
            d = g.seg_seg_dist((x1,y1),(x2,y2),(t["x1"],t["y1"]),(t["x2"],t["y2"]))
            if d - half - t["w"]/2 < CL - 1e-6:
                bad.append(f"[{net}] seg ({x1},{y1})-({x2},{y2}) vs track [{t['net']}] "
                           f"({t['x1']},{t['y1']})-({t['x2']},{t['y2']}): gap {d-half-t['w']/2:.3f} mm")
        for v in vias:
            if v["net"] == net: continue
            d = g.seg_dist(v["x"], v["y"], x1, y1, x2, y2)
            if d - half - v["d"]/2 < CL - 1e-6:
                bad.append(f"[{net}] seg ({x1},{y1})-({x2},{y2}) vs via [{v['net']}] "
                           f"({v['x']},{v['y']}): gap {d-half-v['d']/2:.3f} mm")
        for rj, net2, w2, layer2, s2 in proposed:
            if rj <= ri or net2 == net or layer2 != layer: continue
            d = g.seg_seg_dist((x1,y1),(x2,y2),(s2[0],s2[1]),(s2[2],s2[3]))
            if d - half - w2/2 < CL - 1e-6:
                bad.append(f"[{net}] seg {s} vs PROPOSED [{net2}] {s2}: gap {d-half-w2/2:.3f} mm")
    if verbose:
        if bad:
            print(f"{len(bad)} VIOLATION(S):")
            for b in sorted(set(bad)): print("  " + b)
        else:
            print(f"OK: {len(proposed)} segments across {len(routes)} routes, no violations.")
    return bad

def endpoints_on_pads(routes):
    """Warn if a route endpoint does not land inside a pad of its own net."""
    pads, _, vias = g.load()
    out = []
    for r in routes:
        for (x, y) in (r["pts"][0], r["pts"][-1]):
            hit = any(p["net"] == r["net"] and p["x0"]-0.001 <= x <= p["x1"]+0.001
                      and p["y0"]-0.001 <= y <= p["y1"]+0.001 for p in pads)
            if not hit:
                out.append(f"[{r['net']}] endpoint ({x},{y}) is not on a pad of its net")
    return out

def emit(routes):
    """Print the flat segment list for pushing through MCP."""
    for r in routes:
        for (x1,y1,x2,y2) in segs(r):
            print(json.dumps({"net": r["net"], "layer": r.get("layer","F.Cu"),
                              "w": r.get("w",0.25),
                              "x1": round(x1,4), "y1": round(y1,4),
                              "x2": round(x2,4), "y2": round(y2,4)}))
