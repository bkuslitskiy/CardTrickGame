// Minimal zero-dependency static file server for the test harness.
// Serves the repository root over HTTP so Playwright tests can load
// index.html / engine.js / ai.js / ui.js / styles.css with a real origin.
//
// Usage: node tools/static-server.js [port]

const http = require('http');
const fs   = require('fs');
const path = require('path');

const PORT = Number(process.argv[2]) || 4173;
const ROOT = path.resolve(__dirname, '..');

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js':   'application/javascript; charset=utf-8',
  '.css':  'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png':  'image/png',
  '.svg':  'image/svg+xml',
  '.ico':  'image/x-icon',
  '.txt':  'text/plain; charset=utf-8',
  '.map':  'application/json; charset=utf-8',
};

function safeJoin(root, urlPath) {
  // Decode and strip any query string. Malformed escapes (e.g. "/%") throw —
  // treat them as a bad request rather than crashing the server.
  let decoded;
  try {
    decoded = decodeURIComponent(urlPath.split('?')[0]);
  } catch (e) {
    return null;
  }
  // Normalize to prevent .. traversal escaping ROOT. Compare against
  // root + separator so a sibling directory like "<root>-other" can't pass
  // a bare startsWith(root) check.
  const resolved = path.normalize(path.join(root, decoded));
  if (resolved !== root && !resolved.startsWith(root + path.sep)) return null;
  // Deny dotfiles and dot-directories (.git, .vscode, …) anywhere in the path.
  const rel = path.relative(root, resolved);
  if (rel.split(path.sep).some(part => part.startsWith('.'))) return null;
  return resolved;
}

const server = http.createServer((req, res) => {
  const parsed = new URL(req.url, 'http://localhost');
  let target = safeJoin(ROOT, parsed.pathname || '/');

  if (target === null) {
    res.writeHead(403); res.end('Forbidden'); return;
  }

  // Default to /index.html for the root request.
  if (parsed.pathname === '/' || parsed.pathname === '') {
    target = path.join(ROOT, 'index.html');
  }

  fs.stat(target, (err, stat) => {
    if (err || !stat.isFile()) {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end(`Not found: ${parsed.pathname}`);
      return;
    }
    const ext = path.extname(target).toLowerCase();
    res.writeHead(200, {
      'Content-Type': MIME[ext] || 'application/octet-stream',
      'Cache-Control': 'no-store',
    });
    fs.createReadStream(target).pipe(res);
  });
});

// Bind to loopback only — this serves the whole repo, so it must never be
// reachable from the LAN.
server.listen(PORT, '127.0.0.1', () => {
  // eslint-disable-next-line no-console
  console.log(`[static-server] serving ${ROOT} on http://localhost:${PORT}`);
});
