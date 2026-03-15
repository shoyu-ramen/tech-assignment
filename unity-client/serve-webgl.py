#!/usr/bin/env python3
"""Serves a Unity WebGL build with correct MIME types and Content-Encoding for .gz files."""

import http.server
import os
import sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8090
DIRECTORY = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Builds", "WebGL")


class UnityWebGLHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DIRECTORY, **kwargs)

    def end_headers(self):
        path = self.translate_path(self.path)
        if path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        # CORS for local dev
        self.send_header("Access-Control-Allow-Origin", "*")
        # Disable caching for local dev
        self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
        super().end_headers()

    def guess_type(self, path):
        if path.endswith(".wasm") or path.endswith(".wasm.gz"):
            return "application/wasm"
        if path.endswith(".js") or path.endswith(".js.gz"):
            return "application/javascript"
        if path.endswith(".data") or path.endswith(".data.gz"):
            return "application/octet-stream"
        return super().guess_type(path)


print(f"Serving Unity WebGL from {DIRECTORY} on http://localhost:{PORT}")
http.server.HTTPServer(("", PORT), UnityWebGLHandler).serve_forever()
