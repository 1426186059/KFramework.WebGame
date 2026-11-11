// 本地资源服务器（Node，仅开发用）：把客户端资源目录通过 HTTP 暴露给浏览器页面。
// 目的：资源动辄 8GB+，不复制进 wwwroot（省磁盘），让浏览器直接从源目录按需拉取。
//
// 用法（一般由同目录 start-assets.bat 调用，root 指向仓库根的 Mir2Res/）：
//   node asset-server.js --port 5080 --root "<Mir2 资源根目录>"
//
// 路径映射（root = Mir2Res）：
//   /MyRes/Localization/... -> MyRes/Localization/  语言包（Chinese.json）
//   /MyRes/Sound/...        -> MyRes/Sound/         音效 wav + SoundList.lst
//   /MyRes/Login/...        -> MyRes/Login/         登录 UI 美术 PNG
//   /resource/...           -> resource/            图集 png / json / fnt 与 default.res.json
//   /assets/...             -> assets/              地图瓦片 jpg
//   旧 /Localization/、/Sound/ 前缀自动回落到 /MyRes/ 同名目录（兼容历史写法）
//   其余路径原样相对 root 解析（/MyRes/ 不再重写到 /hot_update_res/）
// 特性：CORS（跨域必需）、Range（为按需分段加载预留）、防目录穿越。

const http = require('http');
const fs = require('fs');
const path = require('path');
const url = require('url');

function parseArgs() {
  const args = process.argv.slice(2);
  const opt = { port: 5080, root: process.cwd() };
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--port') opt.port = parseInt(args[++i], 10);
    else if (args[i] === '--root') opt.root = args[++i];
  }
  return opt;
}

const { port, root } = parseArgs();
const rootResolved = path.resolve(root);

const CORS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': '*',
  'Access-Control-Allow-Methods': 'GET,HEAD,OPTIONS',
  'Access-Control-Expose-Headers': 'Content-Length,Content-Range',
};

const MIME = {
  '.lib': 'application/octet-stream',
  '.zl': 'application/octet-stream',
  '.db': 'application/octet-stream',
  '.dat': 'application/octet-stream',
  '.map': 'application/octet-stream',
  '.wav': 'audio/wav',
  '.mp3': 'audio/mpeg',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.bmp': 'image/bmp',
  '.ini': 'text/plain',
  '.txt': 'text/plain; charset=utf-8',
  '.lst': 'text/plain; charset=utf-8',
  '.fnt': 'text/plain; charset=utf-8',
  '.json': 'application/json',
};

const server = http.createServer((req, res) => {
  if (req.method === 'OPTIONS') {
    res.writeHead(204, CORS);
    return res.end();
  }

  let pathname;
  try {
    pathname = decodeURIComponent(url.parse(req.url).pathname);
  } catch (e) {
    res.writeHead(400, CORS);
    return res.end('bad request');
  }

  // 客户端 ContentManager 用 http.BaseAddress + "/xxx" 拼串，可能带出 "//MyRes/..."，
  // 折叠开头的连续斜杠，避免被 url.parse 当成 protocol-relative 的 host 而丢掉目录名。
  pathname = '/' + pathname.replace(/^\/+/, '');
  // 语言包 / 音效已移到 Mir2Res/MyRes/ 下，旧 URL 继续兼容（客户端已改用 /MyRes/...）。
  pathname = pathname.replace(/^\/(Localization|Sound)\//i, '/MyRes/$1/');
  const filePath = path.resolve(rootResolved, '.' + path.normalize(pathname));

  if (!filePath.startsWith(rootResolved)) {
    res.writeHead(403, CORS);
    return res.end('forbidden');
  }

  fs.stat(filePath, (err, st) => {
    if (err || !st.isFile()) {
      res.writeHead(404, { ...CORS, 'Content-Type': 'text/plain; charset=utf-8' });
      return res.end('not found: ' + pathname);
    }

    const type = MIME[path.extname(filePath).toLowerCase()] || 'application/octet-stream';
    const headers = { ...CORS, 'Content-Type': type, 'Accept-Ranges': 'bytes' };
    const range = req.headers.range;

    if (range) {
      const m = /bytes=(\d*)-(\d*)/.exec(range);
      if (m) {
        const start = m[1] ? parseInt(m[1], 10) : 0;
        const end = m[2] ? parseInt(m[2], 10) : st.size - 1;
        if (start >= st.size || start > end) {
          res.writeHead(416, { ...headers, 'Content-Range': `bytes */${st.size}` });
          return res.end();
        }
        const last = Math.min(end, st.size - 1);
        res.writeHead(206, {
          ...headers,
          'Content-Range': `bytes ${start}-${last}/${st.size}`,
          'Content-Length': last - start + 1,
        });
        const stream = fs.createReadStream(filePath, { start, end: last });
        stream.on('error', (e) => { if (!res.headersSent) res.writeHead(500, CORS); res.end('read error: ' + e.message); });
        return stream.pipe(res);
      }
    }

    res.writeHead(200, { ...headers, 'Content-Length': st.size });
    const stream = fs.createReadStream(filePath);
    stream.on('error', (e) => { if (!res.headersSent) res.writeHead(500, CORS); res.end('read error: ' + e.message); });
    stream.pipe(res);
  });
});

server.listen(port, '127.0.0.1', () => {
  console.log('[assets] root = ' + rootResolved);
  console.log('[assets] url  = http://127.0.0.1:' + port + '/   (/MyRes/{Localization,Sound,Login}/, /resource/, /assets/ 按 root 下同名目录解析；旧 /Localization/、/Sound/ 自动兼容)');
});
