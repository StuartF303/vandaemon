import re, sys, math
path = r"C:/Projects/vandaemon/hw/VDDimmer/VANDIMMER-4CH2A.kicad_pcb"
src = open(path, encoding="utf-8").read()

def find_blocks(text, opener):
    """Yield (start,end) of balanced s-expr blocks beginning with opener."""
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

fps = {}
for s,e in find_blocks(src, "\t(footprint "):
    blk = src[s:e]
    ref = re.search(r'\(property "Reference" "([^"]+)"', blk)
    at  = re.search(r'\n\t\t\(at ([-\d.]+) ([-\d.]+)(?: ([-\d.]+))?\)', blk)
    if not ref or not at: continue
    r = ref.group(1)
    fx, fy = float(at.group(1)), float(at.group(2))
    rot = float(at.group(3) or 0)
    pads = []
    for ps, pe in find_blocks(blk, "\n\t\t(pad "):
        pb = blk[ps:pe]
        num = re.match(r'\s*\(pad "([^"]*)"', pb)
        pat = re.search(r'\(at ([-\d.]+) ([-\d.]+)', pb)
        net = re.search(r'\(net "([^"]*)"\)', pb)
        sz  = re.search(r'\(size ([-\d.]+) ([-\d.]+)\)', pb)
        if not pat: continue
        px, py = float(pat.group(1)), float(pat.group(2))
        a = math.radians(-rot)
        gx = fx + px*math.cos(a) - py*math.sin(a)
        gy = fy + px*math.sin(a) + py*math.cos(a)
        pads.append(dict(num=num.group(1) if num else "", x=round(gx,4), y=round(gy,4),
                         net=net.group(1) if net else "",
                         w=float(sz.group(1)) if sz else 0, h=float(sz.group(2)) if sz else 0))
    fps[r] = dict(x=fx, y=fy, rot=rot, pads=pads)

if len(sys.argv) > 1 and sys.argv[1] == "net":
    target = sys.argv[2]
    for r, f in sorted(fps.items()):
        for p in f["pads"]:
            if p["net"] == target:
                print(f"{r}.{p['num']:<4} {p['x']:9.3f} {p['y']:9.3f}  {p['net']}")
elif len(sys.argv) > 1 and sys.argv[1] == "near":
    cx, cy, rad = float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4])
    for r, f in sorted(fps.items()):
        d = math.hypot(f["x"]-cx, f["y"]-cy)
        if d <= rad:
            print(f"{r:<5} {f['x']:8.3f} {f['y']:8.3f} rot{f['rot']:6.1f}  d={d:6.2f}")
else:
    for r in sys.argv[1:]:
        f = fps.get(r)
        if not f: print(f"{r}: NOT FOUND"); continue
        print(f"{r} @ ({f['x']}, {f['y']}) rot {f['rot']}")
        for p in f["pads"]:
            print(f"   pad {p['num']:<4} ({p['x']:9.3f}, {p['y']:9.3f})  {p['w']}x{p['h']}  net={p['net']}")
