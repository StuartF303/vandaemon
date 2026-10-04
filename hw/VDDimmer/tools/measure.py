import importlib.util, math, os, re
SP = os.path.dirname(os.path.abspath(__file__))
s=importlib.util.spec_from_file_location("geom",SP+"/geom.py"); g=importlib.util.module_from_spec(s); s.loader.exec_module(g)
pads,tracks,vias=g.load()
B=r"C:/Projects/vandaemon/hw/VDDimmer/VANDIMMER-4CH2A.kicad_pcb"
def P(ref,num):
    for p in pads:
        if p["ref"]==ref and str(p["num"])==str(num): return p
    return None

print("=== 1. Cin hot loop (C23 -> U2 pin3/pin4 -> C23) ===")
u2v,u2g = P("U2","3"), P("U2","4")   # VIN, GND
c23a,c23b = P("C23","1"), P("C23","2")
pts=[(u2v["cx"],u2v["cy"]),(u2g["cx"],u2g["cy"]),(c23b["cx"],c23b["cy"]),(c23a["cx"],c23a["cy"])]
A=abs(sum(pts[i][0]*pts[(i+1)%4][1]-pts[(i+1)%4][0]*pts[i][1] for i in range(4)))/2
for n,p in [("U2.3 VIN",u2v),("U2.4 GND",u2g),("C23.1",c23a),("C23.2",c23b)]:
    print(f"   {n:12s} ({p['cx']:.3f},{p['cy']:.3f})")
print(f"   loop area = {A:.2f} mm^2   (gate: < 15)")
xs=[p[0] for p in pts]; ys=[p[1] for p in pts]
inloop=[v for v in vias if min(xs)-0.4<=v["x"]<=max(xs)+0.4 and min(ys)-0.4<=v["y"]<=max(ys)+0.4]
print(f"   vias inside/touching loop bbox = {len(inloop)}  (gate: 0)")
for v in inloop: print(f"     !! via {v['net']} at ({v['x']},{v['y']})")

print("\n=== 2. Switch node (SW) clearance to other nets ===")
sw=[t for t in tracks if t["net"]=="SW_NODE" or t["net"].endswith("/SW")]
if not sw: sw=[t for t in tracks if "SW" in t["net"].upper() and "NODE" in t["net"].upper() or t["net"].upper().endswith("SW")]
print(f"   SW segments: {len(sw)}  nets={set(t['net'] for t in sw)}")
worst=[]
for t in sw:
    for o in tracks:
        if o["net"]==t["net"] or o["layer"]!=t["layer"]: continue
        d=g.seg_seg_dist((t["x1"],t["y1"]),(t["x2"],t["y2"]),(o["x1"],o["y1"]),(o["x2"],o["y2"]))-t["w"]/2-o["w"]/2
        worst.append((d,o["net"],f"({o['x1']},{o['y1']})-({o['x2']},{o['y2']})"))
worst.sort()
for d,n,loc in worst[:8]: print(f"   {d:7.3f} mm to {n:24s} {loc}")

print("\n=== 3. FB divider (R21/R22/C27) vs L1 and SW ===")
for ref in ("R21","R22","C27"):
    ps=[p for p in pads if p["ref"]==ref]
    if not ps: print(f"   {ref}: not found"); continue
    dl=min(math.hypot(p["cx"]-q["cx"],p["cy"]-q["cy"]) for p in ps for q in pads if q["ref"]=="L1")
    ds=min(g.seg_rect_dist(t["x1"],t["y1"],t["x2"],t["y2"],p)-t["w"]/2
           for p in ps for t in sw) if sw else float('nan')
    print(f"   {ref}: {dl:6.2f} mm to L1 (pad-centre),  {ds:6.2f} mm to SW copper")

print("\n=== 4. Bottom-layer copper that is NOT pour or stitching ===")
bt=[t for t in tracks if t["layer"]=="B.Cu"]
tot=sum(math.hypot(t["x2"]-t["x1"],t["y2"]-t["y1"]) for t in bt)
print(f"   {len(bt)} B.Cu track segments, {tot:.2f} mm total")
bynet={}
for t in bt:
    L=math.hypot(t["x2"]-t["x1"],t["y2"]-t["y1"]); bynet.setdefault(t["net"],[0,0,1e9])
    bynet[t["net"]][0]+=L; bynet[t["net"]][1]+=1
u2x,u2y=P("U2","3")["cx"],P("U2","3")["cy"]
for t in bt:
    d=g.seg_dist(u2x,u2y,t["x1"],t["y1"],t["x2"],t["y2"]); bynet[t["net"]][2]=min(bynet[t["net"]][2],d)
for n,(L,c,d) in sorted(bynet.items(), key=lambda kv:-kv[1][0]):
    print(f"   {n:22s} {c:2d} seg {L:7.2f} mm   nearest approach to U2: {d:6.2f} mm")

print("\n=== 5. Zones (filled polygons) ===")
src=open(B,encoding='utf-8').read()
rows=[]
for zs,ze in g.blocks(src, "\n\t(zone"):
    zb=src[zs:ze]
    net=re.search(r'\(net_name "([^"]*)"\)', zb) or re.search(r'\(net "([^"]*)"\)', zb)
    lay=re.search(r'\(layer "([^"]+)"\)', zb)
    pri=re.search(r'\(priority (\d+)\)', zb)
    net=net.group(1) if net else "?"
    lay=lay.group(1) if lay else "?"
    pri=int(pri.group(1)) if pri else 0
    area=0.0; n=0
    for fs,fe in g.blocks(zb, "\n\t\t(filled_polygon"):
        pts=[(float(a),float(b)) for a,b in re.findall(r'\(xy ([-\d.]+) ([-\d.]+)\)', zb[fs:fe])]
        if len(pts)<3: continue
        area+=abs(sum(pts[i][0]*pts[(i+1)%len(pts)][1]-pts[(i+1)%len(pts)][0]*pts[i][1]
                      for i in range(len(pts))))/2
        n+=1
    rows.append((lay,net,pri,n,area))
for lay,net,pri,n,area in sorted(rows, key=lambda r:(r[0],-r[4])):
    print(f"   {lay:6s} {net:14s} prio {pri}  {n:4d} island(s)  {area:9.1f} mm^2")
