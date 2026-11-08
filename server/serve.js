// Локальный HTTPS-сервер для WebGL-сборки Unity (без decompression fallback).
// Запуск:  node server/serve.js [папка_сборки] [порт]
// Сертификаты: server/cert.pem, server/key.pem (создаются mkcert, см. README).
const https = require('https');
const fs = require('fs');
const path = require('path');
const os = require('os');

const root = path.resolve(process.argv[2] || path.join(__dirname, '..', 'Builds', 'WebGL_Gzip'));
const port = Number(process.argv[3] || 8443);

const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.wasm': 'application/wasm',
  '.data': 'application/octet-stream',
  '.json': 'application/json',
  '.css': 'text/css',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.mem': 'application/octet-stream',
  '.symbols.json': 'application/json',
};

function contentType(file) {
  // file.wasm.gz -> .wasm ; file.framework.js.br -> .js
  const base = file.replace(/\.(gz|br)$/, '');
  return types[path.extname(base)] || 'application/octet-stream';
}

const server = https.createServer({
  key: fs.readFileSync(path.join(__dirname, 'key.pem')),
  cert: fs.readFileSync(path.join(__dirname, 'cert.pem')),
}, (req, res) => {
  let urlPath = decodeURIComponent(req.url.split('?')[0]);
  if (urlPath.endsWith('/')) urlPath += 'index.html';
  const file = path.join(root, urlPath);
  if (!file.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) { res.writeHead(404); return res.end('Not found'); }
    // ETag/Last-Modified нужны кэшу Unity (Data Caching, IndexedDB): без них он не видит,
    // что сборка изменилась, и подсовывает старый .data к новому .wasm ("level0 is corrupted").
    const etag = `"${st.size.toString(16)}-${Math.floor(st.mtimeMs).toString(16)}"`;
    const headers = {
      'Content-Type': contentType(file),
      'Content-Length': st.size,
      'Cache-Control': 'no-cache',
      'ETag': etag,
      'Last-Modified': st.mtime.toUTCString(),
    };
    // Без decompression fallback браузер сам распаковывает — нужен Content-Encoding
    if (file.endsWith('.gz')) headers['Content-Encoding'] = 'gzip';
    if (file.endsWith('.br')) headers['Content-Encoding'] = 'br';
    if (req.headers['if-none-match'] === etag) {
      res.writeHead(304, { 'ETag': etag, 'Cache-Control': 'no-cache' });
      return res.end();
    }
    res.writeHead(200, headers);
    fs.createReadStream(file).pipe(res);
    console.log(`${req.socket.remoteAddress} ${req.method} ${urlPath} ${st.size}`);
  });
});

server.listen(port, '0.0.0.0', () => {
  console.log(`Serving ${root}`);
  console.log(`  https://localhost:${port}/`);
  for (const ifs of Object.values(os.networkInterfaces())) {
    for (const i of ifs) if (i.family === 'IPv4' && !i.internal) console.log(`  https://${i.address}:${port}/   <- открыть на телефоне (та же Wi-Fi сеть)`);
  }
});
