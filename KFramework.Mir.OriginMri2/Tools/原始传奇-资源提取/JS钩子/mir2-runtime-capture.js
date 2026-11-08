/* 《原始传奇》线上运行时数据抓取钩子
 *
 * 用途：在【已登录】的游戏页面 DevTools Console 里执行，把页面正常收发的数据导出来，
 *       用于确认是否存在 mapmonster / mapxxx 配置表，以及抓取「每个地图的怪物」数据。
 * 时机：最好在进入游戏前注入（Tampermonkey @run-at document-start 亦可）；
 *       若已进入游戏，注入后切换地图 / 重登 / 打开悬赏面板，可触发协议重新下发。
 *
 * 命令：
 *   __mir2dump()                导出 JSON 文件
 *   __mir2probe()               探测 configUrl 下的独立配置文件
 *   __mir2probe(['xxx.json'])   追加自定义文件名探测
 *
 * 只记录本页面已经正常收发的请求，不改包、不重放、不绕过登录。
 */
(function () {
  'use strict';
  var HOST = window;
  var TAG = '[MIR2-CAP]';

  // 自动定位游戏主 frame：带 js_gameVars / cm / RES 的那个 window 才是 Egret 游戏页。
  // 顶层外壳页、本地 127.0.0.1 复刻客户端都没有这三个对象。
  function findGameWindow() {
    var list = [HOST];
    function collect(win, depth) {
      try {
        for (var i = 0; i < win.frames.length; i++) {
          var f = win.frames[i];
          if (list.indexOf(f) < 0) { list.push(f); if (depth < 2) collect(f, depth + 1); }
        }
      } catch (e) {}
    }
    collect(HOST, 0);
    var best = null, bestScore = 0;
    list.forEach(function (f) {
      var s = 0, href = '(cross-origin)';
      try { href = f.location.href; } catch (e) {}
      try { if (f.js_gameVars && Object.keys(f.js_gameVars).length) s += 4; } catch (e) {}
      try { if (f.cm && typeof f.cm === 'object') s += 3; } catch (e) {}
      try { if (f.RES && typeof f.RES.getResByUrl === 'function') s += 2; } catch (e) {}
      console.log(TAG, 'frame score=' + s, href);
      if (s > bestScore) { bestScore = s; best = f; }
    });
    return { win: best, score: bestScore };
  }

  var found = findGameWindow();
  if (found.score === 0) {
    console.error(TAG, '没找到游戏主 frame：可访问的 window 上都没有 js_gameVars / cm / RES。');
    console.error(TAG, '请确认 ① 当前是【线上原始传奇页游】页面，不是本地 127.0.0.1 复刻客户端；② 若游戏在 iframe，把 DevTools Console 顶部上下文下拉框切到游戏那个 frame 再执行。');
    try {
      console.log(TAG, '当前页面', location.href, 'iframes',
        Array.from(document.querySelectorAll('iframe')).map(function (x) { return x.src; }));
    } catch (e) {}
    return;
  }

  var W = found.win;
  console.log(TAG, '已定位游戏 frame:', (function () { try { return W.location.href; } catch (e) { return '?'; } })());
  var cap = W.__mir2cap = {
    at: new Date().toISOString(),
    href: location.href,
    gameVars: null,
    tables: [],
    mapTables: [],
    urls: [],
    ws: [],
    probes: []
  };
  var log = function () {
    var a = Array.prototype.slice.call(arguments);
    console.log.apply(console, [TAG].concat(a));
  };

  // 1) 运行时注入参数：configUrl（数据服务器）、resUrl、resVersion、resver
  try {
    cap.gameVars = JSON.parse(JSON.stringify(W.js_gameVars || {}));
    log('js_gameVars', cap.gameVars);
  } catch (e) { log('gameVars fail', e); }

  // 2) 全局配置对象 cm（ConfigManager 实例）：直接列出游戏实际加载的所有表名
  try {
    var c = W.cm;
    if (!c && W.ConfigManager && typeof W.ConfigManager.ins === 'function') c = W.ConfigManager.ins();
    if (c) {
      cap.tables = Object.keys(c).filter(function (k) { return c[k] && typeof c[k] === 'object'; }).sort();
      cap.mapTables = cap.tables.filter(function (k) { return /map|mon/i.test(k); });
      log('tables=' + cap.tables.length, 'map/mon tables:', cap.mapTables.join(','));
    } else {
      log('cm 不在 window 上，候选全局:', Object.keys(W).filter(function (k) {
        return /^(cm|config|gd|emIns|net|ResUrl|Vars)$/i.test(k);
      }).join(','));
    }
  } catch (e) { log('tables fail', e); }

  function remember(kind, url) {
    try {
      cap.urls.push({ kind: kind, url: String(url), t: Date.now() });
      if (cap.urls.length > 4000) cap.urls.shift();
    } catch (e) {}
  }

  // 3) Egret 资源加载器：配置分片 / 地图逻辑文件都走 RES.getResByUrl
  try {
    if (W.RES && typeof W.RES.getResByUrl === 'function') {
      var origRes = W.RES.getResByUrl;
      W.RES.getResByUrl = function (url) {
        remember('RES', url);
        return origRes.apply(this, arguments);
      };
      log('RES.getResByUrl hooked');
    }
  } catch (e) { log('RES hook fail', e); }

  // 4) fetch / XHR
  try {
    var origFetch = W.fetch;
    if (origFetch) {
      W.fetch = function (input) {
        remember('fetch', typeof input === 'string' ? input : (input && input.url));
        return origFetch.apply(this, arguments);
      };
    }
    var X = W.XMLHttpRequest;
    if (X && X.prototype.open) {
      var origOpen = X.prototype.open;
      X.prototype.open = function (m, u) { remember('xhr', u); return origOpen.apply(this, arguments); };
    }
    log('fetch/XHR hooked');
  } catch (e) { log('fetch/XHR hook fail', e); }

  // 5) WebSocket 帧：保留前 4KB base64 + 32 字节头部 hex + 总长度
  function enc(d) {
    if (typeof d === 'string') return { t: 's', n: d.length, s: d.slice(0, 4096) };
    var u8 = null;
    if (d instanceof ArrayBuffer) u8 = new Uint8Array(d);
    else if (d && d.buffer) u8 = new Uint8Array(d.buffer, d.byteOffset || 0, d.byteLength);
    if (!u8) return { t: '?', n: 0 };
    var head = Array.from(u8.subarray(0, 32), function (x) {
      return x.toString(16).padStart(2, '0');
    }).join('');
    var part = u8.subarray(0, Math.min(u8.length, 4096));
    var bin = '';
    for (var i = 0; i < part.length; i++) bin += String.fromCharCode(part[i]);
    return { t: 'b', n: u8.length, head: head, b64: btoa(bin) };
  }

  try {
    var NWS = W.WebSocket;
    if (NWS) {
      function HookedWS(url, protocols) {
        var ws = protocols === undefined ? new NWS(url) : new NWS(url, protocols);
        var rec = { url: String(url), opened: Date.now(), sent: [], recv: [] };
        cap.ws.push(rec);
        if (ws.send) {
          var origSend = ws.send.bind(ws);
          ws.send = function (d) { rec.sent.push(enc(d)); return origSend(d); };
        }
        try { ws.addEventListener('message', function (ev) { rec.recv.push(enc(ev.data)); }, true); } catch (e) {}
        return ws;
      }
      HookedWS.prototype = NWS.prototype;
      ['CONNECTING', 'OPEN', 'CLOSING', 'CLOSED'].forEach(function (k) {
        try { HookedWS[k] = NWS[k]; } catch (e) {}
      });
      W.WebSocket = HookedWS;
      log('WebSocket hooked');
    }
  } catch (e) { log('ws hook fail', e); }

  // 6) 主动探测数据服务器上的独立配置文件
  W.__mir2probe = async function (extra) {
    var gv = cap.gameVars || {};
    var base = gv.configUrl;
    if (!base) { log('no configUrl'); return cap.probes; }
    var ver = gv.resVersion || '';
    var names = [
      '154.bin', 'map/154.bin', '103.bin', 'map/103.bin',
      'mapmonster.json', 'mapMonster.json', 'mapmonster' + ver + '.json',
      '6config' + ver + '.json', '7config' + ver + '.json'
    ].concat(extra || []);
    for (var i = 0; i < names.length; i++) {
      var url = base + names[i];
      try {
        var r = await fetch(url, { method: 'GET' });
        var buf = await r.arrayBuffer();
        cap.probes.push({ url: url, status: r.status, bytes: buf.byteLength });
        log('probe', r.status, buf.byteLength, url);
      } catch (e) {
        cap.probes.push({ url: url, error: String(e) });
      }
    }
    return cap.probes;
  };

  // 7) 导出
  W.__mir2dump = function () {
    var blob = new Blob([JSON.stringify(cap, null, 2)], { type: 'application/json' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'mir2-capture-' + Date.now() + '.json';
    a.click();
    var frames = cap.ws.reduce(function (n, w) { return n + w.recv.length; }, 0);
    log('dumped:', cap.urls.length, 'urls,', cap.ws.length, 'sockets,', frames,
        'recv frames,', cap.probes.length, 'probes');
  };

  // 把导出/探测入口挂回 Console 所在的 window，跨 frame 也能直接调用
  HOST.__mir2cap = cap;
  HOST.__mir2dump = W.__mir2dump;
  HOST.__mir2probe = W.__mir2probe;

  log('ready. 进比奇省 / 打开悬赏面板触发 108004-108005，然后执行 __mir2dump()');
})();
