/* MIR2 真实刷怪事件采集 —— 在游戏 frame 的 DevTools Console 粘贴执行
 *
 * 为什么不能直接用 67001 的 addMonsters 生成刷怪点：
 *   67001 顶层含 exitIdList，是「视野进出增量同步」包，x/y 是实体【当前位置】。
 *   玩家跑动、怪物移动都会让同一只怪反复出现在不同坐标，按 (mid,x,y) 去重
 *   会把移动轨迹当成几百个刷怪点（上一版 CSV 里火尾黑狐王连成一条线就是这个原因）。
 *
 * 正解：hook emIns.createEntity(type, lid, bean)。
 *   逆向自 main10.25487.1.min.js：
 *     addMonster(e) → emIns.createEntity(e.type, e.id, e)，其中 e 是 MonsterObject；
 *     type===4 只在怪物【被创建】时调用一次，移动更新走别的路径。
 *     bean.monsterid = cm.monster 的 id（怪物种类）
 *     bean.bornX / bornY = 服务端下发的出生格坐标（addMonster 用它 *48/*32 转像素）
 *     bean.id / bean.lid = 实体唯一 id（用于跨帧去重）
 *   地图取 gd.map.config.id / gd.map.curMapId；
 *   刷新间隔取 cm.monster[mid].reliveDelay（毫秒）。
 *
 * 用法：
 *   1) 粘贴本脚本 → 打印 [MIR2-SPAWN] ready，确认 hooked:true
 *   2) 正常跑图/切图即可：每 1.5 秒自动扫描 _monsterDic 补入视野内已有实体，
 *      不必等 30 分钟刷新；也可 __mir2spawns.scan() 立刻补扫一次
 *   3) 多切几张图后执行 __mir2spawns.export() → 打印并复制 CSV，Ctrl+V 发回
 *      __mir2spawns.json() 看明细 / status() 看进度 / reset() 清空重来
 *   注意：服务端只下发视野内实体，一张图要采全需在该图里跑动几圈。
 *   重复粘贴本脚本不会丢已采事件，也不会叠加钩子。
 */
(function () {
  var W = window;
  var prev = W.__mir2spawns;
  var EVENTS = prev && prev.events ? prev.events : [];
  var INDEX = {};
  EVENTS.forEach(function (e) { INDEX[String(e.mapId) + '#' + e.lid] = 1; });
  var TIMER = null;
  if (prev && prev.timer) { try { clearInterval(prev.timer); } catch (e) {} }

  function mapInfo() {
    var gm = (W.gd && W.gd.map) || {};
    var cfg = gm.config || gm.mapConfig || gm._mapConfig || gm.cfg || {};
    var id = cfg.id;
    if (id === undefined || id === null) {
      ['curMapId', 'prepareMapid', 'mapId', 'mapID', '_mapId', 'mapid', 'ctostageid'].forEach(function (k) {
        if ((id === undefined || id === null) && gm[k] !== undefined && gm[k] !== null) id = gm[k];
      });
    }
    var cm = W.cm || {};
    var mc = (cm.map && cm.map[id]) || {};
    return {
      id: (id === undefined || id === null) ? null : id,
      name: cfg.name || mc.name || null,
      width: mc.width === undefined ? null : mc.width,
      height: mc.height === undefined ? null : mc.height
    };
  }
  function monsterCfg(mid) {
    try { return (W.cm && W.cm.monster && W.cm.monster[mid]) || {}; } catch (e) { return {}; }
  }
  function toStr(v) {
    try { return (v && v.toString) ? v.toString() : String(v); } catch (e) { return String(v); }
  }
  function num(v) {
    try { return (v && v.toNumber) ? v.toNumber() : (typeof v === 'number' ? v : null); } catch (e) { return null; }
  }

  function onSpawn(type, lid, bean, fallbackLid) {
    try {
      if (Number(type) !== 4 || !bean) return;
      var mi = mapInfo();
      var lidStr = toStr(bean.id !== undefined ? bean.id :
                         (bean.lid !== undefined ? bean.lid :
                          (fallbackLid !== undefined ? fallbackLid : lid)));
      var key = String(mi.id) + '#' + lidStr;
      if (INDEX[key]) return;                 // 同一只怪只记第一次出现（= 刷出位置）
      INDEX[key] = 1;
      var mid = bean.monsterid !== undefined ? bean.monsterid :
                bean.monid !== undefined ? bean.monid : bean.mid;
      var mc = monsterCfg(mid);
      EVENTS.push({
        time: new Date().toISOString(),
        mapId: mi.id, mapName: mi.name,
        width: mi.width, height: mi.height,
        mid: mid,
        name: mc.name || bean.name || null,
        x: bean.bornX !== undefined ? bean.bornX : (bean.gridX !== undefined ? bean.gridX : bean.x),
        y: bean.bornY !== undefined ? bean.bornY : (bean.gridY !== undefined ? bean.gridY : bean.y),
        lid: lidStr,
        hp: num(bean.truehp !== undefined ? bean.truehp : bean.hp),
        reliveDelayMs: mc.reliveDelay === undefined ? null : mc.reliveDelay,
        level: mc.level === undefined ? null : mc.level,
        mtype: mc.type === undefined ? null : mc.type
      });
    } catch (e) {}
  }

  // 粘贴脚本前已在场景里的怪不会触发 createEntity（魂灵守卫 30 分钟才刷一次，
  // 干等是不现实的）。所以定时扫描 emIns._monsterDic 把视野内已有实体补进来：
  // 用户只需正常跑图/切图，新进入视野的怪会自动入库。
  function pickBean(e) {
    var c = [e._monsterObject, e.monsterObject, e.gameObject, e];
    for (var i = 0; i < c.length; i++) {
      var b = c[i];
      if (b && (b.monsterid !== undefined || b.monid !== undefined || b.mid !== undefined)) return b;
    }
    return e || {};
  }
  function harvestExisting() {
    try {
      var d = (W.emIns && W.emIns._monsterDic) || {};
      Object.keys(d).forEach(function (k) {
        var e = d[k];
        if (!e) return;
        onSpawn(4, e.uid || k, pickBean(e), e.uid || k);
      });
    } catch (e) {}
  }

  // 挂 emIns.createEntity；重复粘贴时先恢复原方法，避免闭包叠加导致重复记录
  var HOOKED = false;
  try {
    var em = W.emIns;
    if (em && typeof em.createEntity === 'function') {
      if (em.__mir2spawnOrig) em.createEntity = em.__mir2spawnOrig;
      var orig = em.createEntity;
      em.__mir2spawnOrig = orig;
      em.createEntity = function (type, lid, bean) {
        onSpawn(type, lid, bean);
        return orig.apply(this, arguments);
      };
      HOOKED = true;
    }
  } catch (e) {}

  // 立即补一次 + 每 1.5 秒自动扫描；重复粘贴时旧 timer 已在开头清掉
  harvestExisting();
  TIMER = setInterval(harvestExisting, 1500);

  function clean(s) { return String(s === null || s === undefined ? '' : s).replace(/[,\r\n]/g, ' '); }
  function csv() {
    var lines = ['mapId,mapName,width,height,mid,monsterName,x,y,reliveDelayMs,level,mtype,lid,time'];
    EVENTS.forEach(function (e) {
      lines.push([e.mapId, clean(e.mapName), e.width, e.height, e.mid, clean(e.name),
                  e.x, e.y, e.reliveDelayMs, e.level, e.mtype, e.lid, e.time].join(','));
    });
    return lines.join('\n');
  }
  function status() {
    var maps = {};
    EVENTS.forEach(function (e) { var k = String(e.mapId); maps[k] = (maps[k] || 0) + 1; });
    return { hooked: HOOKED, autoscan: !!TIMER, events: EVENTS.length, maps: maps, currentMap: mapInfo() };
  }
  function emitCsv() {
    var t = csv();
    console.log('[MIR2-SPAWN] ' + JSON.stringify(status()) + '\n' + t);
    try { copy(t); console.log('[MIR2-SPAWN] CSV 已复制到剪贴板，直接 Ctrl+V 发给助手'); }
    catch (e) {}
    return t;
  }

  W.__mir2spawns = {
    events: EVENTS,
    timer: TIMER,
    scan: harvestExisting,
    export: emitCsv,
    json: function () { console.log('[MIR2-SPAWN] JSON\n' + JSON.stringify(EVENTS, null, 2)); return EVENTS; },
    reset: function () {
      EVENTS.length = 0;
      Object.keys(INDEX).forEach(function (k) { delete INDEX[k]; });
      return 0;
    },
    status: status
  };
  console.log('[MIR2-SPAWN] ready', status());
})();
