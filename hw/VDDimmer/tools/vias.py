import importlib.util, math, json
SP = os.path.dirname(os.path.abspath(__file__))
s=importlib.util.spec_from_file_location("geom",SP+"/geom.py"); g=importlib.util.module_from_spec(s); s.loader.exec_module(g)
pads,tracks,vias = g.load()
CL=0.2; EDGE=0.5
BX0,BY0,BX1,BY1 = 100.0,60.0,200.0,140.0

def clear(x,y,d,net="GND"):
    r=d/2
    if not (x-r-EDGE>=BX0 and y-r-EDGE>=BY0 and x+r+EDGE<=BX1 and y+r+EDGE<=BY1): return False
    for p in pads:
        if p["net"]==net: continue
        dx=max(p["x0"]-x, 0, x-p["x1"]); dy=max(p["y0"]-y, 0, y-p["y1"])
        if math.hypot(dx,dy)-r < CL: return False
    for t in tracks:
        if t["net"]==net: continue
        if g.seg_dist(x,y,t["x1"],t["y1"],t["x2"],t["y2"]) - r - t["w"]/2 < CL: return False
    for v in vias:
        if v["net"]==net: continue
        if math.hypot(v["x"]-x, v["y"]-y) - r - v["d"]/2 < CL: return False
    return True

placed=[]
def try_place(x,y,d):
    for vx,vy,vd in placed:
        if math.hypot(vx-x,vy-y) < (vd+d)/2 + 0.4: return False
    if clear(x,y,d):
        placed.append((round(x,3),round(y,3),d)); return True
    return False

# 1. thermal ring around U2's GND pad, all outside the Cin hot loop (south-east of pin 4)
for (x,y) in [(178.4,76.2),(179.6,76.9),(180.8,77.6),(177.5,77.9),(176.3,78.5),(178.7,78.9),(179.9,79.6)]:
    try_place(x,y,0.8)
ring = len(placed)

# 2. perimeter stitching about every 10 mm
per=[]
for x in range(102,199,10): per += [(x,62.0),(x,138.0)]
for y in range(64,139,10): per += [(102.0,y),(198.0,y)]
for x,y in per: try_place(float(x),float(y),0.8)
perim = len(placed)-ring

# 3. one via beside every GND pad that has no GND via within 1.6 mm
gnd=[p for p in pads if p["net"]=="GND"]
seen=set()
for p in gnd:
    key=(round(p["cx"],2),round(p["cy"],2))
    if key in seen: continue
    seen.add(key)
    if any(math.hypot(vx-p["cx"],vy-p["cy"])<1.6 for vx,vy,_ in placed): continue
    ok=False
    for rad in (1.0,1.3,1.6,2.0,2.4):
        for a in range(0,360,15):
            x=p["cx"]+rad*math.cos(math.radians(a)); y=p["cy"]+rad*math.sin(math.radians(a))
            if try_place(x,y,0.8): ok=True; break
        if ok: break
    if not ok: print(f"  no room for a GND via near {p['ref']}.{p['num']} ({p['cx']:.2f},{p['cy']:.2f})")
print(f"thermal ring {ring}, perimeter {perim}, pad vias {len(placed)-ring-perim}, TOTAL {len(placed)}")
open(SP+"/gnd_vias.json","w").write(json.dumps(placed))
