const http = require('http');
const fs = require('fs');
const path = require('path');

const ROOTS = [
  path.resolve(__dirname, 'KFramework.Example3/wwwroot'),            // index.html, jsengine, content
  path.resolve(__dirname, 'KFramework.Example3/bin/Release/net10.0/wwwroot') // _framework runtime
];
const PORT = 54041;
const MIME = {
  '.html':'text/html', '.js':'text/javascript', '.mjs':'text/javascript',
  '.wasm':'application/wasm', '.json':'application/json', '.css':'text/css',
  '.png':'image/png', '.jpg':'image/jpeg', '.webp':'image/webp',
  '.data':'application/octet-stream', '.dat':'application/octet-stream',
  '.lib':'application/octet-stream', '.txt':'text/plain', '.map':'application/json',
  '.atlas':'application/octet-stream', '.wav':'audio/wav', '.cl':'application/octet-stream'
};

function resolve(url){
  let rel = decodeURIComponent(url.split('?')[0]);
  if (rel === '/') rel = '/index.html';
  for (const root of ROOTS){
    const fp = path.join(root, rel);
    if (fp.startsWith(root) && fs.existsSync(fp) && fs.statSync(fp).isFile()) return fp;
  }
  return null;
}

http.createServer((req,res)=>{
  const fp = resolve(req.url);
  if (!fp) { res.writeHead(404); res.end('not found: '+req.url); return; }
  fs.readFile(fp,(err,buf)=>{
    if (err) { res.writeHead(404); res.end('err'); return; }
    const ext = path.extname(fp).toLowerCase();
    res.writeHead(200, {'Content-Type': MIME[ext]||'application/octet-stream', 'Access-Control-Allow-Origin':'*'});
    res.end(buf);
  });
}).listen(PORT, ()=>console.log('serving at http://localhost:'+PORT+' (roots: '+ROOTS.length+')'));
