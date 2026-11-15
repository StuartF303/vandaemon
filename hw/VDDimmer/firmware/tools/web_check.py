#!/usr/bin/env python3
"""Exercise the board's HTTP API and report pass/fail per check.

Usage:
    web_check.py --host 192.168.1.115          # a real board
    web_check.py --host localhost:8765         # tools/web_mock.py

Reads the current levels and names first and restores them at the end, so it
is safe to run against the board in the van: the only visible effect is
channel 1 going to a test level for a moment.

Exit 0 if every check passed, 1 otherwise, 2 if the board could not be reached.
Standard library only; run it with the PlatformIO venv's python like the other
tools, or any Python 3.8+.
"""
import argparse
import json
import sys
import urllib.error
import urllib.request


class Api:
    def __init__(self, host, timeout):
        self.base = "http://" + host
        self.timeout = timeout

    def call(self, method, path, body=None, raw=None):
        data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
        req = urllib.request.Request(self.base + path, data=data, method=method)
        if data is not None:
            req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as r:
                return r.status, self._json(r.read())
        except urllib.error.HTTPError as e:
            return e.code, self._json(e.read())

    @staticmethod
    def _json(b):
        try:
            return json.loads(b.decode("utf-8"))
        except Exception:
            return None

    def get(self, path):
        return self.call("GET", path)

    def post(self, path, body=None, raw=None):
        return self.call("POST", path, body, raw)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--host", required=True, help="board IP[:port] or mock host:port")
    ap.add_argument("--timeout", type=float, default=5.0)
    args = ap.parse_args()
    api = Api(args.host, args.timeout)

    results = []

    def check(name, ok, detail=""):
        results.append(ok)
        print("%s  %s%s" % ("PASS" if ok else "FAIL", name, ("  -- " + detail) if detail else ""))

    try:
        code, state = api.get("/api/state")
    except Exception as exc:
        print("cannot reach %s: %s" % (args.host, exc))
        return 2
    check("GET /api/state 200 with 4 levels and 4 names",
          code == 200 and isinstance(state, dict)
          and isinstance(state.get("levels"), list) and len(state["levels"]) == 4
          and isinstance(state.get("names"), list) and len(state["names"]) == 4,
          "code %s body %s" % (code, state))
    if code != 200 or not isinstance(state, dict):
        return 1
    orig_levels = list(state["levels"])
    orig_names = list(state["names"])
    orig_devname = state.get("deviceName", "")

    code, st = api.get("/status.json")
    check("GET /status.json 200 with build and fw 1.1.0",
          code == 200 and isinstance(st, dict) and bool(st.get("build")) and st.get("fw") == "1.1.0",
          "code %s fw %s build %s" % (code, st and st.get("fw"), st and st.get("build")))
    code, st2 = api.get("/api/status")
    check("GET /api/status same shape", code == 200 and isinstance(st2, dict) and st2.get("t") == "status")

    code, r = api.post("/api/channel/1", {"level": 77})
    _, s2 = api.get("/api/state")
    check("POST /api/channel/1 level 77 applied",
          code == 200 and r and r.get("level") == 77 and s2 and s2["levels"][1] == 77,
          "code %s reply %s now %s" % (code, r, s2 and s2["levels"]))

    code, r = api.post("/api/channel/1", {"level": 300})
    _, s3 = api.get("/api/state")
    check("POST level 300 -> 400, unchanged", code == 400 and s3 and s3["levels"][1] == 77, "code %s" % code)

    code, r = api.post("/api/channel/1", {"level": "abc"})
    check("POST level 'abc' -> 400", code == 400, "code %s" % code)

    code, r = api.post("/api/channel/7", {"level": 1})
    check("POST /api/channel/7 -> 400", code == 400, "code %s" % code)

    code, r = api.post("/api/channel/1", raw=b"not json")
    check("POST non-JSON body -> 400", code == 400, "code %s" % code)

    code, r = api.post("/api/channel/1", raw=b'{"level":1,"pad":"' + b"x" * 600 + b'"}')
    check("POST 600-byte body -> 413", code == 413, "code %s" % code)

    code, r = api.post("/api/names", {"deviceName": "T", "names": ["a", "b", "c"]})
    _, s4 = api.get("/api/state")
    check("POST 3 names -> 400, names unchanged", code == 400 and s4 and s4["names"] == orig_names, "code %s" % code)

    code, r = api.post("/api/names", {"deviceName": "T", "names": ["x" * 21, "", "", ""]})
    check("POST 21-byte name -> 400", code == 400, "code %s" % code)

    euro = "€" * 5   # 15 bytes of UTF-8
    code, r = api.post("/api/names", {"deviceName": orig_devname, "names": [euro, "Kitchen", "Bedroom", ""]})
    _, s5 = api.get("/api/state")
    check("POST UTF-8 names -> 200 and read back identical",
          code == 200 and s5 and s5["names"] == [euro, "Kitchen", "Bedroom", ""],
          "code %s names %s" % (code, s5 and s5["names"]))

    code, r = api.get("/nothing")
    check("GET /nothing -> 404", code == 404, "code %s" % code)

    code, r = api.post("/api/wifi", {})
    check("POST /api/wifi -> 501 (phase 2)", code == 501, "code %s" % code)

    # Restore.
    api.post("/api/names", {"deviceName": orig_devname, "names": orig_names})
    api.post("/api/channel/1", {"level": orig_levels[1]})
    _, final = api.get("/api/state")
    check("restored original names and level",
          final and final["names"] == orig_names and final["levels"][1] == orig_levels[1])

    passed = sum(results)
    print("%d/%d passed" % (passed, len(results)))
    return 0 if passed == len(results) else 1


if __name__ == "__main__":
    sys.exit(main())
