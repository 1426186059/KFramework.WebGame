/* 抓包摘要 v2：读取已安装的 __mir2cap，打印并复制到剪贴板
 * 用法：在同一个游戏 frame 的 Console 粘贴执行，然后 Ctrl+V 发给助手。
 * 新增：读出 ResUrl.Data / resVersion 真实值，并以真实 base 探测配置文件
 *       （用 0config<ver>.json 作对照，判断 404 是"文件不存在"还是"base/版本猜错"）。
 * 不会重复安装钩子。
 */
(async function () {
  var cap = window.__mir2cap;
  if (!cap) {
    console.error('[MIR2-CAP] 没找到 __mir2cap；请先重新粘贴 mir2-runtime-capture.js');
    return;
  }
  var gv = cap.gameVars || {};

  // 全局对象里可能藏着真实的数据服务器地址
  var globals = {};
  ['ResUrl', 'Vars', 'gd', 'emIns', 'net', 'NetSocket', 'SocketManager', 'cm'].forEach(function (k) {
    try {
      var v = window[k];
      if (!v) return;
      if (typeof v !== 'object') { globals[k] = String(v).slice(0, 120); return; }
      globals[k] = Object.keys(v).slice(0, 40).reduce(function (o, kk) {
        try {
          var x = v[kk];
          o[kk] = (x && typeof x === 'object') ? '[object]' : String(x).slice(0, 120);
        } catch (e) {}
        return o;
      }, {});
    } catch (e) {}
  });

  // 真实 base：静态配置在 CDN（js_gameVars.cdnServer），不是登录页 /12/ 目录。
  // 之前用 configUrl('') 落到 /12/ 导致所有探测 404，属于 base 猜错而非文件不存在。
  var base = gv.cdnServer || gv.configUrl || '';
  var absBase = base;
  try { absBase = new URL(base || '.', location.href).href; } catch (e) {}
  if (absBase && absBase.indexOf('?') < 0 && absBase.slice(-1) !== '/') absBase += '/';

  var ver = gv.resVersion || '';
  var names = [
    '0config' + ver + '.json',           // 对照：这个通了才说明 base/版本对
    '1config' + ver + '.json',
    '5config' + ver + '.json',
    '6config' + ver + '.json',
    '7config' + ver + '.json',
    '154.bin',
    'map/154.bin',
    '103.bin',
    'mapmonster.json',
    'mapMonster.json',
    'mapmonster' + ver + '.json',
    'map' + ver + '.dat',
    'map10.25487.1.dat'
  ];
  var probes = [];
  for (var i = 0; i < names.length; i++) {
    var url = absBase + names[i];
    try {
      var r = await fetch(url, { method: 'GET' });
      var buf = await r.arrayBuffer();
      probes.push({ url: url, status: r.status, bytes: buf.byteLength });
      console.log('[MIR2-CAP] probe', r.status, buf.byteLength, url);
    } catch (e) {
      probes.push({ url: url, error: String(e) });
    }
  }
  cap.probes = probes;

  // gameVars 里 allManifast 有几千条资源，会把关键输出挤爆，只保留数量
  var slim = { allManifastCount: gv.allManifast ? Object.keys(gv.allManifast).length : 0 };
  Object.keys(gv).forEach(function (k) {
    if (k === 'allManifast' || k === 'banHao' || k === 'createRoleMsg') return;
    var v = gv[k];
    slim[k] = (v && typeof v === 'object') ? '[object]' : v;
  });

  var interesting = (cap.urls || [])
    .filter(function (x) { return /config|map|mon|\.bin/i.test(String(x.url)); })
    .map(function (x) { return x.kind + ' ' + x.url; });
  var summary = {
    href: location.href,
    gameVars: slim,
    globals: globals,
    dataBase: absBase,
    tableCount: (cap.tables || []).length,
    mapTables: cap.mapTables,
    allTables: cap.tables,
    interestingUrls: Array.from(new Set(interesting)).slice(0, 200),
    ws: (cap.ws || []).map(function (w) {
      return {
        url: w.url,
        sent: w.sent.length,
        recv: w.recv.length,
        recvHeads: w.recv.slice(0, 3).map(function (f) { return f.head || ''; })
      };
    }),
    probes: probes
  };
  var text = JSON.stringify(summary, null, 2);
  console.log('[MIR2-CAP] SUMMARY\n' + text);
  try {
    copy(text);
    console.log('[MIR2-CAP] 摘要已复制到剪贴板，直接 Ctrl+V 发给助手');
  } catch (e) {
    try {
      navigator.clipboard.writeText(text);
      console.log('[MIR2-CAP] 摘要已复制到剪贴板');
    } catch (e2) {
      console.log('[MIR2-CAP] 请手动复制上面的 SUMMARY 文本');
    }
  }
})();
