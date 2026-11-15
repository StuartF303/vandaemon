#!/usr/bin/env python3
"""Desktop stand-in for the board's web interface.

Serves src/web/index.html at / and implements the same JSON API as
src/web.cpp with in-memory state, the same validation and the same status
codes -- so the page can be exercised in a browser, and tools/web_check.py
run, without a board. It is also the executable description of the API: if
the firmware and this disagree, one of them has a bug.

    python tools/web_mock.py            # http://localhost:8765/
    python tools/web_mock.py --port 9000

Standard library only.
"""
import argparse
import json
import os
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, HTTPServer
from socketserver import ThreadingMixIn


class ThreadingHTTPServer(ThreadingMixIn, HTTPServer):
    """http.server has this from Python 3.7; defined here so 3.6 works too."""
    daemon_threads = True

HERE = os.path.dirname(os.path.abspath(__file__))
PAGE = os.path.join(HERE, "..", "src", "web", "index.html")

BODY_MAX = 512
CH_NAME_MAX = 20
DEVNAME_MAX = 39
HOST_MAX = 63
USER_MAX = 32
PASS_MAX = 64

STARTED = time.time()
LOCK = threading.Lock()
STATE = {
    "deviceId": "vandimmer-mock00",
    "deviceName": "Van lights (mock)",
    "levels": [180, 100, 0, 0],
    "names": ["Front", "Kitchen", "", ""],
    "mqtt": {"host": "192.168.1.50", "port": 1883, "user": "", "pass": "", "up": False, "lastResult": "unreachable"},
    "rebootAt": None,
}


def status_obj():
    return {
        "t": "status",
        "deviceId": STATE["deviceId"],
        "deviceName": STATE["deviceName"],
        "fw": "1.1.0",
        "build": "mock0000 2026-11-15T00:00:00Z",
        "uptime": int(time.time() - STARTED),
        "wifi": {"ssid": "JCDammVan", "up": True, "rssi": -58, "ip": "127.0.0.1"},
        "mqtt": {k: STATE["mqtt"][k] for k in ("host", "port", "up", "lastResult")},
        "vin": 12.6,
        "tempC": 34.5,
        "derate": 1.0,
        "overTemp": False,
        "freeHeap": 212000,
    }


def is_str(v, max_bytes):
    return isinstance(v, str) and len(v.encode("utf-8")) <= max_bytes


def is_int(v):
    return isinstance(v, int) and not isinstance(v, bool)


class Handler(BaseHTTPRequestHandler):
    server_version = "vandimmer-mock/1.1.0"

    def log_message(self, fmt, *args):
        sys.stderr.write("%s %s\n" % (self.command, self.path))

    # ---- replies
    def reply(self, code, obj, ctype="application/json"):
        data = json.dumps(obj).encode() if ctype == "application/json" else obj
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def error(self, code, msg):
        self.reply(code, {"error": msg})

    def body(self):
        """Returns (dict, None) or (None, (code, msg)) after validation."""
        n = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(n) if n else b""
        if len(raw) > BODY_MAX:
            return None, (413, "too large")
        try:
            doc = json.loads(raw.decode("utf-8"))
        except Exception:
            return None, (400, "bad json")
        if not isinstance(doc, dict):
            return None, (400, "bad json")
        return doc, None

    # ---- routes
    def do_GET(self):
        path = self.path.split("?")[0]
        if path == "/":
            with open(PAGE, "rb") as fh:
                self.reply(200, fh.read(), "text/html; charset=utf-8")
        elif path == "/api/state":
            with LOCK:
                self.reply(200, {k: STATE[k] for k in ("deviceId", "deviceName", "levels", "names")})
        elif path in ("/api/status", "/status.json"):
            with LOCK:
                self.reply(200, status_obj())
        elif path in ("/api/wifi", "/api/wifi/scan"):
            self.error(501, "phase 2")
        else:
            self.error(404, "not found")

    def do_POST(self):
        path = self.path.split("?")[0]
        if path.startswith("/api/channel/"):
            return self.post_channel(path[len("/api/channel/"):])
        if path == "/api/names":
            return self.post_names()
        if path == "/api/mqtt":
            return self.post_mqtt()
        if path == "/api/identify":
            self.body()
            sys.stderr.write("[mock] identify: status LED would blink\n")
            return self.reply(200, {"ok": True})
        if path == "/api/reboot":
            self.body()
            sys.stderr.write("[mock] reboot requested\n")
            return self.reply(200, {"ok": True})
        if path in ("/api/wifi", "/api/wifi/scan"):
            self.body()
            return self.error(501, "phase 2")
        self.body()
        self.error(404, "not found")

    def post_channel(self, arg):
        if not arg.isdigit() or len(arg) > 9 or int(arg) > 3:
            self.body()
            return self.error(400, "bad channel")
        ch = int(arg)
        doc, err = self.body()
        if err:
            return self.error(*err)
        level = doc.get("level")
        if not is_int(level) or level < 0 or level > 255:
            return self.error(400, "bad level")
        with LOCK:
            STATE["levels"][ch] = level
        self.reply(200, {"ok": True, "level": level})

    def post_names(self):
        doc, err = self.body()
        if err:
            return self.error(*err)
        dev = doc.get("deviceName")
        if not is_str(dev, DEVNAME_MAX):
            return self.error(400, "bad deviceName")
        names = doc.get("names")
        if not isinstance(names, list) or len(names) != 4:
            return self.error(400, "names must have 4 entries")
        for n in names:
            if not is_str(n, CH_NAME_MAX):
                return self.error(400, "name too long")
        with LOCK:
            STATE["deviceName"] = dev
            STATE["names"] = list(names)
        self.reply(200, {"ok": True})

    def post_mqtt(self):
        doc, err = self.body()
        if err:
            return self.error(*err)
        host = doc.get("host")
        if not is_str(host, HOST_MAX) or host == "":
            return self.error(400, "bad host")
        port = doc.get("port", 1883)
        if not is_int(port) or port < 1 or port > 65535:
            return self.error(400, "bad port")
        user = doc.get("user", "")
        if not is_str(user, USER_MAX):
            return self.error(400, "bad user")
        pw = doc.get("pass")
        if pw is not None and not is_str(pw, PASS_MAX):
            return self.error(400, "bad pass")
        with LOCK:
            m = STATE["mqtt"]
            m["host"], m["port"], m["user"] = host, port, user
            if pw:
                m["pass"] = pw
            # Pretend: the stock mock broker lives at 192.168.1.50.
            m["up"] = host == "192.168.1.50"
            m["lastResult"] = "connected" if m["up"] else "unreachable"
        self.reply(200, {"ok": True})


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", type=int, default=8765)
    ap.add_argument("--bind", default="127.0.0.1")
    args = ap.parse_args()
    if not os.path.exists(PAGE):
        print("page not found at %s" % PAGE)
        return 2
    srv = ThreadingHTTPServer((args.bind, args.port), Handler)
    print("VANDIMMER mock at http://%s:%d/  (Ctrl+C to stop)" % (args.bind, args.port), flush=True)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
