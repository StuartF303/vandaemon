"""Board geometry model: pad rectangles + track segments, with clearance queries."""
import os, re, math

BOARD = r"C:/Projects/vandaemon/hw/VDDimmer/VANDIMMER-4CH2A.kicad_pcb"

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

def load(path=BOARD):
    src = open(path, encoding="utf-8").read()
    pads = []   # dict(ref,num,net,x0,y0,x1,y1,layers)
    for s,e in blocks(src, "\t(footprint "):
        blk = src[s:e]
        ref = re.search(r'\(property "Reference" "([^"]+)"', blk)
        at  = re.search(r'\n\t\t\(at ([-\d.]+) ([-\d.]+)(?: ([-\d.]+))?\)', blk)
        if not ref or not at: continue
        r=ref.group(1); fx,fy=float(at.group(1)),float(at.group(2)); rot=float(at.group(3) or 0)
        a=math.radians(-rot); ca,sa=math.cos(a),math.sin(a)
        for ps,pe in blocks(blk, "\n\t\t(pad "):
            pb = blk[ps:pe]
            num = re.match(r'\s*\(pad "([^"]*)"', pb)
            pat = re.search(r'\(at ([-\d.]+) ([-\d.]+)(?: ([-\d.]+))?\)', pb)
            sz  = re.search(r'\(size ([-\d.]+) ([-\d.]+)\)', pb)
            net = re.search(r'\(net "([^"]*)"\)', pb)
            lay = re.search(r'\(layers ([^)]*)\)', pb)
            if not pat or not sz: continue
            px,py=float(pat.group(1)),float(pat.group(2))
            prot=float(pat.group(3) or 0)
            gx = fx+px*ca-py*sa; gy = fy+px*sa+py*ca
            w,h = float(sz.group(1)), float(sz.group(2))
            # KiCad writes a pad's (at x y angle) as an ABSOLUTE angle in the
            # board file -- it already includes the footprint rotation. So the
            # size swap depends on the pad angle alone, not fp_rot + pad_rot.
            tot = prot % 180
            if abs(tot-90) < 1:  w,h = h,w
            pads.append(dict(ref=r, num=num.group(1) if num else "",
                             net=net.group(1) if net else "",
                             x0=gx-w/2, y0=gy-h/2, x1=gx+w/2, y1=gy+h/2,
                             cx=gx, cy=gy, w=w, h=h,
                             layers=lay.group(1) if lay else ""))
    tracks=[]
    for s,e in blocks(src, "\n\t(segment"):
        b=src[s:e]
        st=re.search(r'\(start ([-\d.]+) ([-\d.]+)\)', b)
        en=re.search(r'\(end ([-\d.]+) ([-\d.]+)\)', b)
        wd=re.search(r'\(width ([-\d.]+)\)', b)
        ly=re.search(r'\(layer "([^"]+)"\)', b)
        nt=re.search(r'\(net "([^"]*)"\)', b)
        if not st: continue
        tracks.append(dict(x1=float(st.group(1)), y1=float(st.group(2)),
                           x2=float(en.group(1)), y2=float(en.group(2)),
                           w=float(wd.group(1)), layer=ly.group(1),
                           net=nt.group(1) if nt else ""))
    vias=[]
    for s,e in blocks(src, "\n\t(via"):
        b=src[s:e]
        at=re.search(r'\(at ([-\d.]+) ([-\d.]+)\)', b)
        sz=re.search(r'\(size ([-\d.]+)\)', b)
        nt=re.search(r'\(net "([^"]*)"\)', b)
        if not at: continue
        vias.append(dict(x=float(at.group(1)), y=float(at.group(2)),
                         d=float(sz.group(1)) if sz else 0.8,
                         net=nt.group(1) if nt else ""))
    return pads, tracks, vias

def seg_dist(px,py, x1,y1,x2,y2):
    dx,dy = x2-x1, y2-y1
    if dx==0 and dy==0: return math.hypot(px-x1,py-y1)
    t = max(0,min(1, ((px-x1)*dx+(py-y1)*dy)/(dx*dx+dy*dy)))
    return math.hypot(px-(x1+t*dx), py-(y1+t*dy))

def seg_seg_dist(a,b,c,d):
    """min distance between segments a-b and c-d (2D points)."""
    def cross(o,p,q): return (p[0]-o[0])*(q[1]-o[1])-(p[1]-o[1])*(q[0]-o[0])
    d1,d2,d3,d4 = cross(c,d,a), cross(c,d,b), cross(a,b,c), cross(a,b,d)
    if ((d1>0)!=(d2>0)) and ((d3>0)!=(d4>0)): return 0.0
    return min(seg_dist(a[0],a[1],c[0],c[1],d[0],d[1]),
               seg_dist(b[0],b[1],c[0],c[1],d[0],d[1]),
               seg_dist(c[0],c[1],a[0],a[1],b[0],b[1]),
               seg_dist(d[0],d[1],a[0],a[1],b[0],b[1]))

def seg_rect_dist(x1,y1,x2,y2, r):
    """min distance from segment to axis-aligned rect r (dict x0,y0,x1,y1). 0 if intersecting."""
    rx0,ry0,rx1,ry1 = r['x0'],r['y0'],r['x1'],r['y1']
    # point inside rect?
    for (px,py) in ((x1,y1),(x2,y2)):
        if rx0<=px<=rx1 and ry0<=py<=ry1: return 0.0
    edges=[((rx0,ry0),(rx1,ry0)),((rx1,ry0),(rx1,ry1)),((rx1,ry1),(rx0,ry1)),((rx0,ry1),(rx0,ry0))]
    return min(seg_seg_dist((x1,y1),(x2,y2),e[0],e[1]) for e in edges)
