import importlib.util, math, json, re
SP = os.path.dirname(os.path.abspath(__file__))
s=importlib.util.spec_from_file_location("geom",SP+"/geom.py"); g=importlib.util.module_from_spec(s); s.loader.exec_module(g)
pads,tracks,vias=g.load()
d=json.load(open(SP+"/drc-report.json",encoding='utf-8'))
# collect the GND pads DRC says are unconnected
want=set()
for v in d['unconnected_items']:
    ds=[i.get('description','') for i in v.get('items',[])]
    if not any('[GND]' in x for x in ds): continue
    for x in ds:
        m=re.search(r'[Pp]ad (\S+) \[GND\] of (\S+)', x)
        if m: want.add((m.group(2), m.group(1)))
gv=[v for v in vias if v["net"]=="GND"]
print(f"{len(want)} unconnected GND pads")
out=[]
for ref,num in sorted(want):
    ps=[p for p in pads if p["ref"]==ref and str(p["num"])==num]
    if not ps: print("  ?? no pad", ref, num); continue
    p=min(ps, key=lambda q: min(math.hypot(v["x"]-q["cx"], v["y"]-q["cy"]) for v in gv))
    v=min(gv, key=lambda v: math.hypot(v["x"]-p["cx"], v["y"]-p["cy"]))
    # clamp the pad end to the pad rectangle nearest the via
    sx=min(max(v["x"], p["x0"]+0.15), p["x1"]-0.15)
    sy=min(max(v["y"], p["y0"]+0.15), p["y1"]-0.15)
    out.append({"ref":ref,"num":num,"pts":[(round(sx,3),round(sy,3)),(v["x"],v["y"])],
                "len":round(math.hypot(v["x"]-sx, v["y"]-sy),2)})
for o in out: print(f"  {o['ref']}.{o['num']:<4} {o['pts'][0]} -> {o['pts'][1]}  {o['len']} mm")
json.dump(out, open(SP+"/gndstub.json","w"))
