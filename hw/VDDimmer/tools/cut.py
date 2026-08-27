import sys, importlib.util
spec = importlib.util.spec_from_file_location("geom", r"C:/Users/stuart/AppData/Local/Temp/claude/C--Projects-vandaemon/d2672728-61f3-45dc-9027-42ef7f22c948/scratchpad/geom.py")
g = importlib.util.module_from_spec(spec); spec.loader.exec_module(g)
pads, tracks, vias = g.load()

X = float(sys.argv[1]); Y0=float(sys.argv[2]); Y1=float(sys.argv[3])
CL = 0.2; W = 0.25
blocked=[]
for p in pads:
    if p['x0']-CL-W/2 <= X <= p['x1']+CL+W/2:
        blocked.append((max(Y0,p['y0']-CL-W/2), min(Y1,p['y1']+CL+W/2), p['ref']+'.'+p['num']))
blocked=[b for b in blocked if b[0]<b[1]]
blocked.sort()
free=[]; cur=Y0
merged=[]
for a,b,r in blocked:
    if merged and a<=merged[-1][1]: merged[-1][1]=max(merged[-1][1],b); merged[-1][2]+=","+r
    else: merged.append([a,b,r])
for a,b,r in merged:
    if a>cur: free.append((cur,a))
    cur=max(cur,b)
if cur<Y1: free.append((cur,Y1))
print(f"cut x={X}, y {Y0}..{Y1}   (clearance {CL}, trace {W})")
print("blocked:")
for a,b,r in merged: print(f"   {a:8.3f}..{b:8.3f}  {r}")
print("free windows (usable centreline range) and trace capacity:")
tot=0
for a,b in free:
    span=b-a
    n = int((span - CL) // (W+CL)) if span>CL else 0
    tot+=n
    print(f"   {a:8.3f}..{b:8.3f}  span {span:6.3f} mm -> {n} traces")
print(f"TOTAL capacity across cut: {tot} traces")
