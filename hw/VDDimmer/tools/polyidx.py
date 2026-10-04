"""Fast point-in-polygon for KiCad zone fills.

A filled_polygon is a keyhole polygon: thermal-relief holes and pad clearances are
carved out and stitched to the outer boundary by zero-width cut lines. Testing a
pad's CENTRE therefore reports "not in the zone" for every thermally-relieved pad,
which is why a naive centre test over-splits nets. Sample the pad's perimeter too --
the thermal spokes land on the pad edge.

Builds a scanline interval index so each query is a binary search.
"""
import bisect, math

class PolyIndex:
    def __init__(self, pts, step=0.05):
        self.step = step
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        self.x0, self.x1 = min(xs), max(xs)
        self.y0, self.y1 = min(ys), max(ys)
        nrows = max(1, int((self.y1 - self.y0) / step) + 2)
        buckets = [[] for _ in range(nrows)]
        n = len(pts)
        for i in range(n):
            ax, ay = pts[i]; bx, by = pts[(i + 1) % n]
            if ay == by: continue
            lo, hi = (ay, by) if ay < by else (by, ay)
            r0 = max(0, int((lo - self.y0) / step))
            r1 = min(nrows - 1, int((hi - self.y0) / step) + 1)
            for r in range(r0, r1 + 1):
                buckets[r].append((ax, ay, bx, by))
        self.rows = []
        for r in range(nrows):
            y = self.y0 + r * step
            xs_ = []
            for ax, ay, bx, by in buckets[r]:
                if (ay > y) != (by > y):
                    xs_.append((bx - ax) * (y - ay) / (by - ay) + ax)
            xs_.sort()
            self.rows.append(xs_)

    def point_in(self, x, y):
        if not (self.x0 <= x <= self.x1 and self.y0 <= y <= self.y1): return False
        r = int(round((y - self.y0) / self.step))
        if r < 0 or r >= len(self.rows): return False
        xs = self.rows[r]
        return bisect.bisect_right(xs, x) % 2 == 1

    def any_in(self, points):
        return any(self.point_in(x, y) for x, y in points)

def rect_samples(x0, y0, x1, y1, step=0.05):
    """Centre + perimeter of an axis-aligned rect, so thermal spokes are seen."""
    pts = [((x0 + x1) / 2, (y0 + y1) / 2)]
    nx = max(1, int((x1 - x0) / step)); ny = max(1, int((y1 - y0) / step))
    for i in range(nx + 1):
        x = x0 + (x1 - x0) * i / nx
        pts.append((x, y0)); pts.append((x, y1))
    for j in range(ny + 1):
        y = y0 + (y1 - y0) * j / ny
        pts.append((x0, y)); pts.append((x1, y))
    return pts

def disc_samples(cx, cy, r, n=16):
    pts = [(cx, cy)]
    for i in range(n):
        a = 2 * math.pi * i / n
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts

def seg_samples(x1, y1, x2, y2, step=0.1):
    L = math.hypot(x2 - x1, y2 - y1)
    n = max(1, int(L / step))
    return [(x1 + (x2 - x1) * i / n, y1 + (y2 - y1) * i / n) for i in range(n + 1)]
