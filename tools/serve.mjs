// Um servidor local só para testar o editor: node tools/serve.mjs [porta]  →  http://localhost:8080
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(fileURLToPath(new URL(".", import.meta.url)), "..", "web");
const port = +process.argv[2] || 8080;
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8", ".png": "image/png", ".json": "application/json", ".xml": "application/xml", ".webmanifest": "application/manifest+json", ".gif": "image/gif", ".ico": "image/x-icon" };
createServer(async (req, res) => {
  const path = normalize(decodeURIComponent(new URL(req.url, "http://x").pathname)).replace(/^([/\\])+/, "");
  const file = join(root, path || "index.html");
  if (!file.startsWith(root)) { res.writeHead(403).end(); return; }
  try {
    const data = await readFile(file.endsWith("\\") || file.endsWith("/") ? join(file, "index.html") : file);
    res.writeHead(200, { "Content-Type": types[extname(file)] || "application/octet-stream", "Cache-Control": "no-store" }).end(data);
  } catch { res.writeHead(404).end("not found"); }
}).listen(port, () => console.log("DeskPals Editor: http://localhost:" + port));