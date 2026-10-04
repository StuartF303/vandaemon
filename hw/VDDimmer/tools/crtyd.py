import re, math, sys
path = r"C:/Projects/vandaemon/hw/VDDimmer/VANDIMMER-4CH2A.kicad_pcb"
src = open(path, encoding="utf-8").read()
def blocks(text, opener):
    i = 0
    while True:
        i = text.find(opener, i)
        if i < 0: return
        depth = 0; j = i
        while j < len(text):
            c = text[j]
            if c == '"':
                j += 1
                while j < len(text) and not (text[j] == '"' and text[j-1] != chr(92)): j += 1
            elif c == '(': depth += 1
            elif c == ')':
                depth -= 1
                if depth == 0:
                    yield (i, j+1); break
            j += 1
        i = j+1
out={}
for s,e in blocks(src, "\t(footprint "):
    blk = src[s:e]
    ref = re.search(r'\(property "Reference" "([^"]+)"', blk)
    at  = re.search(r'\n\t\t\(at ([-\d.]+) ([-\d.]+)(?: ([-\d.]+))?\)', blk)
    if not ref or not at: continue
    r=ref.group(1); fx,fy=float(at.group(1)),float(at.group(2)); rot=float(at.group(3) or 0)
    a=math.radians(-rot); ca,sa=math.cos(a),math.sin(a)
    pts=[]
    for gs,ge in blocks(blk, "\n\t\t(fp_"):
        g=blk[gs:ge]
        if 'F.CrtYd' not in g and 'B.CrtYd' not in g: continue
        for m in re.finditer(r'\((?:start|end|mid|xy) ([-\d.]+) ([-\d.]+)\)', g):
            px,py=float(m.group(1)),float(m.group(2))
            pts.append((fx+px*ca-py*sa, fy+px*sa+py*ca))
    if pts:
        xs=[p[0] for p in pts]; ys=[p[1] for p in pts]
        out[r]=(min(xs),min(ys),max(xs),max(ys))
sel=sys.argv[1:] if len(sys.argv)>1 else sorted(out)
for r in sel:
    if r in out:
        x0,y0,x1,y1=out[r]
        print(f"{r:<5} crtyd x {x0:8.3f}..{x1:8.3f}   y {y0:8.3f}..{y1:8.3f}")
    else:
        print(f"{r:<5} no courtyard")
