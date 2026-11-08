/* 《原始传奇》合并捕获钩子：传送点 + 怪物刷新点 —— 在游戏 frame 的 DevTools Console 粘贴执行
 *
 * 合并自：
 *   · capture_map_safe_teleport.js（传送点：钩 gd.map.prepareToEnterMap，按边缘自动判别 传送点/边界走图）
 *   · mir2-capture-spawns.js      （怪物刷新点：钩 emIns.createEntity，按“首次出现=刷出位置”去重）
 *
 * 注：安全区已改为从原始地图 .dat 提取（见 MapInfoEditor 全局设置“修正所有地图安全区”），本脚本不再采集。
 *
 * 用法：
 *   1) 粘贴本脚本 → 打印两模块 ready，确认 teleportHooked / spawnHooked 均为 true；
 *   2) 正常跑图/切图：
 *        - 踩传送点/回城/传送员/边界 → 自动记录 (源地图,坐标,目标地图) 并判别类型；
 *        - 视野内怪物被创建/进入 → 自动记录刷怪点（每 1.5 秒补扫 _monsterDic，不必干等刷新）；
 *   3) 多切几张图后：
 *        __dumpTP()              单独看传送点汇总（__dumpTP(true) 下载 JSON）
 *        __mir2spawns.export()   单独看刷怪点 CSV（复制到剪贴板）
 *        __dumpAll()             合并导出【传送点 + 边界走图 + 怪物刷新点】到一个 JSON（__dumpAll(true) 下载）
 *        __mir2spawns.status()   看进度 / __mir2spawns.reset() 清空刷怪记录
 *        __clearTP()             清空传送点记录
 *   重复粘贴不会丢已采数据，也不会叠加钩子。
 *
 * 传送点判别规则：用 map.config.width/height 反推网格(GRID 48×32)，若 (x,y) 距边缘 <= __TP_EDGE_MARGIN(默认2) 格 → 边界走图；否则 → 传送点。
 * 刷怪点前提：type===4 仅怪物“被创建”时调用一次；bean.monsterid=怪物种类，bean.bornX/bornY=出生格坐标，cm.monster[mid].reliveDelay=刷新间隔(ms)。
 */
(function () {
  'use strict';
  var W = window;
  var TAG = '[MIR2-CAP-ALL]';

  // ===================== A) 传送点（钩 gd.map.prepareToEnterMap）=====================
  var gd = W.gd || {};
  var map = gd.map;
  var TELEPORT_HOOKED = false;

  if (map) {
    // 复用已有记录（重复粘贴不丢数据）；如需清空用 __clearTP()
    if (!W.__teleportLog) W.__teleportLog = [];
    W.__TP_EDGE_MARGIN = W.__TP_EDGE_MARGIN || 2; // 边缘容差（格），可调

    function gridDims() {
      var c = map.config || {};
      if (!c.width || !c.height) return null;
      var GW = 48, GH = 32; // GameDefine.MAP_GRID_WIDTH/HEIGHT
      return { cols: Math.ceil(c.width / GW), rows: Math.ceil(c.height / GH) };
    }
    function readCoord(obj, name) {
      if (!obj) return null;
      if (obj[name] != null) return obj[name];
      if (obj.CurrentLocation && obj.CurrentLocation[name] != null) return obj.CurrentLocation[name];
      return null;
    }
    function playerPos() {
      var p = null;
      try { if (W.emIns && W.emIns.firstPlayer) p = W.emIns.firstPlayer; } catch (e) {}
      if (!p) { try { if (typeof emIns !== 'undefined' && emIns.firstPlayer) p = emIns.firstPlayer; } catch (e) {} }
      if (!p) { try { if (gd.player && gd.player.entity) p = gd.player.entity; } catch (e) {} }
      if (!p) return null;
      var fo = p.fighterObject || p;
      var x = readCoord(fo, 'gridX'); if (x == null) x = readCoord(p, 'gridX');
      var y = readCoord(fo, 'gridY'); if (y == null) y = readCoord(p, 'gridY');
      if (x == null) x = readCoord(fo, 'X'); if (x == null) x = readCoord(p, 'X');
      if (y == null) y = readCoord(fo, 'Y'); if (y == null) y = readCoord(p, 'Y');
      return (x != null && y != null) ? { x: x, y: y } : null;
    }
    function classify(x, y) {
      var d = gridDims();
      if (!d || x == null || y == null) return 'unknown';
      var M = W.__TP_EDGE_MARGIN;
      if (x <= M || y <= M || x >= d.cols - 1 - M || y >= d.rows - 1 - M) return 'border'; // 边界走图
      return 'teleport'; // 地图内部 → 传送点
    }

    if (!W.__tpHooked) {
      var origPTE = map.prepareToEnterMap && map.prepareToEnterMap.bind(map);
      if (origPTE) {
        map.prepareToEnterMap = function (mid) {
          var pos = playerPos();
          var cur = map.config || {};
          var rec = {
            ts: Date.now(),
            sourceMap: (map.curMapId != null ? map.curMapId : cur.id),
            sourceName: cur.name,
            x: pos ? pos.x : null,
            y: pos ? pos.y : null,
            targetMap: mid,
            type: classify(pos ? pos.x : null, pos ? pos.y : null)
          };
          W.__teleportLog.push(rec);
          console.log('[传送点捕获]', JSON.stringify(rec));
          return origPTE(mid);
        };
        W.__tpHooked = true;
        TELEPORT_HOOKED = true;
        console.log(TAG + ' 已钩取 gd.map.prepareToEnterMap —— 记录 (源地图,坐标,目标地图) 并自动判别 传送点/边界。');
      } else {
        console.warn(TAG + ' 未找到 gd.map.prepareToEnterMap，传送点钩取失败；可手动执行 __hookEnter() 改钩 enterMap。');
      }
    } else {
      TELEPORT_HOOKED = true;
      console.log(TAG + ' 传送点已钩取过，跳过重复钩取。');
    }

    W.__clearTP = function () { W.__teleportLog = []; console.log(TAG + ' 已清空传送点记录'); };

    W.__dumpTP = function (download) {
      var norm = function (r) {
        return {
          sourceMap: r.sourceMap != null ? r.sourceMap : r.mapId,
          sourceName: r.sourceName || r.mapName,
          x: r.x != null ? r.x : null, y: r.y != null ? r.y : null,
          targetMap: r.targetMap,
          type: r.type || 'unknown'
        };
      };
      var pairKey = function (r) { return r.sourceMap + '|' + r.targetMap; };
      // 同一 (源地图,目标地图) 只保留 1 条，优先带坐标的
      var best = new Map();
      W.__teleportLog.forEach(function (raw) {
        var r = norm(raw);
        var k = pairKey(r);
        var prev = best.get(k);
        if (!prev) { best.set(k, r); return; }
        var rHas = r.x != null && r.y != null;
        var pHas = prev.x != null && prev.y != null;
        if (rHas && !pHas) best.set(k, r);
        else if (rHas && pHas) best.set(k, prev);
      });
      var teleports = [], borders = [], unknowns = [];
      best.forEach(function (r) {
        if (r.type === 'teleport') teleports.push(r);
        else if (r.type === 'border') borders.push(r);
        else unknowns.push(r);
      });
      var data = {
        传送点: teleports,
        边界走图: borders,
        未判别: unknowns,
        统计: { 传送点: teleports.length, 边界走图: borders.length, 未判别: unknowns.length, 原始记录: W.__teleportLog.length }
      };
      console.log('==== 传送点汇总（传送点 ' + teleports.length + ' / 边界 ' + borders.length + ' / 未判别 ' + unknowns.length + '）====');
      console.log(JSON.stringify(data, null, 2));
      if (download) {
        var blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        var first = W.__teleportLog[0];
        var sid = (first && (first.sourceMap != null ? first.sourceMap : first.mapId)) || '?';
        a.download = 'origin_map_' + sid + '_tp.json';
        document.body.appendChild(a); a.click(); a.remove();
        console.log(TAG + ' 已下载 origin_map_' + sid + '_tp.json');
      }
      return data;
    };

    // 备用：若 prepareToEnterMap 没被触发，可手动执行 __hookEnter() 改钩 enterMap
    W.__hookEnter = function () {
      if (W.__tpHooked) { console.log(TAG + ' 已钩取，跳过'); return; }
      var orig = map.enterMap && map.enterMap.bind(map);
      if (!orig) { console.warn('无 enterMap'); return; }
      map.enterMap = function (mid) {
        var pos = playerPos();
        var cur = map.config || {};
        W.__teleportLog.push({
          ts: Date.now(),
          sourceMap: (map.curMapId != null ? map.curMapId : cur.id),
          sourceName: cur.name, x: pos ? pos.x : null, y: pos ? pos.y : null,
          targetMap: mid, type: classify(pos ? pos.x : null, pos ? pos.y : null)
        });
        return orig(mid);
      };
      W.__tpHooked = true; TELEPORT_HOOKED = true;
      console.log(TAG + ' 已钩取 gd.map.enterMap（备用）');
    };
  } else {
    console.warn(TAG + ' 未找到 gd.map，传送点采集跳过（请确认在原始传奇游戏页面控制台运行）。');
  }

  // ===================== B) 怪物刷新点（钩 emIns.createEntity + 扫描 _monsterDic）=====================
  var EVENTS = (W.__mir2spawns && W.__mir2spawns.events) ? W.__mir2spawns.events : [];
  var INDEX = {};
  EVENTS.forEach(function (e) { INDEX[String(e.mapId) + '#' + e.lid] = 1; });
  var SPAWN_TIMER = null;
  if (W.__mir2spawns && W.__mir2spawns.timer) { try { clearInterval(W.__mir2spawns.timer); } catch (e) {} }
  var SPAWN_HOOKED = false;

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

  var em = W.emIns;
  if (em && typeof em.createEntity === 'function') {
    if (em.__mir2spawnOrig) em.createEntity = em.__mir2spawnOrig;
    var origCreate = em.createEntity;
    em.__mir2spawnOrig = origCreate;
    em.createEntity = function (type, lid, bean) {
      onSpawn(type, lid, bean);
      return origCreate.apply(this, arguments);
    };
    SPAWN_HOOKED = true;
  } else {
    console.warn(TAG + ' 未找到 emIns.createEntity，刷怪点采集跳过。');
  }

  harvestExisting();
  SPAWN_TIMER = setInterval(harvestExisting, 1500);

  function clean(s) { return String(s === null || s === undefined ? '' : s).replace(/[,\r\n]/g, ' '); }
  function spawnCsv() {
    var lines = ['mapId,mapName,width,height,mid,monsterName,x,y,reliveDelayMs,level,mtype,lid,time'];
    EVENTS.forEach(function (e) {
      lines.push([e.mapId, clean(e.mapName), e.width, e.height, e.mid, clean(e.name),
                  e.x, e.y, e.reliveDelayMs, e.level, e.mtype, e.lid, e.time].join(','));
    });
    return lines.join('\n');
  }
  function spawnStatus() {
    var maps = {};
    EVENTS.forEach(function (e) { var k = String(e.mapId); maps[k] = (maps[k] || 0) + 1; });
    return { hooked: SPAWN_HOOKED, autoscan: !!SPAWN_TIMER, events: EVENTS.length, maps: maps, currentMap: mapInfo() };
  }
  W.__mir2spawns = {
    events: EVENTS,
    timer: SPAWN_TIMER,
    scan: harvestExisting,
    export: function () {
      var t = spawnCsv();
      console.log(TAG + ' ' + JSON.stringify(spawnStatus()) + '\n' + t);
      try { copy(t); console.log(TAG + ' 刷怪点 CSV 已复制到剪贴板，直接 Ctrl+V 发给助手'); } catch (e) {}
      return t;
    },
    json: function () { console.log(TAG + ' 刷怪点 JSON\n' + JSON.stringify(EVENTS, null, 2)); return EVENTS; },
    reset: function () {
      EVENTS.length = 0;
      Object.keys(INDEX).forEach(function (k) { delete INDEX[k]; });
      return 0;
    },
    status: spawnStatus
  };

  // ===================== C) 合并导出（传送点 + 边界走图 + 怪物刷新点）=====================
  W.__dumpAll = function (download) {
    // —— 传送点汇总（与 __dumpTP 同逻辑）——
    var norm = function (r) {
      return {
        sourceMap: r.sourceMap != null ? r.sourceMap : r.mapId,
        sourceName: r.sourceName || r.mapName,
        x: r.x != null ? r.x : null, y: r.y != null ? r.y : null,
        targetMap: r.targetMap,
        type: r.type || 'unknown'
      };
    };
    var pairKey = function (r) { return r.sourceMap + '|' + r.targetMap; };
    var best = new Map();
    (W.__teleportLog || []).forEach(function (raw) {
      var r = norm(raw);
      var k = pairKey(r);
      var prev = best.get(k);
      if (!prev) { best.set(k, r); return; }
      var rHas = r.x != null && r.y != null;
      var pHas = prev.x != null && prev.y != null;
      if (rHas && !pHas) best.set(k, r);
      else if (rHas && pHas) best.set(k, prev);
    });
    var teleports = [], borders = [], unknowns = [];
    best.forEach(function (r) {
      if (r.type === 'teleport') teleports.push(r);
      else if (r.type === 'border') borders.push(r);
      else unknowns.push(r);
    });

    var spawns = EVENTS;
    var data = {
      传送点: teleports,
      边界走图: borders,
      未判别: unknowns,
      怪物刷新点: spawns,
      统计: {
        传送点: teleports.length, 边界走图: borders.length, 未判别: unknowns.length,
        怪物刷新点: spawns.length,
        原始传送记录: (W.__teleportLog || []).length,
        原始刷怪事件: spawns.length
      }
    };
    console.log('==== 合并汇总（传送点 ' + teleports.length + ' / 边界 ' + borders.length + ' / 未判别 ' + unknowns.length + ' / 刷怪点 ' + spawns.length + '）====');
    var json = JSON.stringify(data, null, 2);
    console.log(json);
    try { copy(json); console.log(TAG + ' 合并 JSON 已复制到剪贴板（大文件可能失败，可用下载）。'); } catch (e) {}
    if (download) {
      var blob = new Blob([json], { type: 'application/json' });
      var a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      var first = (W.__teleportLog && W.__teleportLog[0]) || (spawns[0]);
      var sid = (first && (first.sourceMap != null ? first.sourceMap : first.mapId)) || '?';
      a.download = 'origin_map_' + sid + '_all.json';
      document.body.appendChild(a); a.click(); a.remove();
      console.log(TAG + ' 已下载 origin_map_' + sid + '_all.json');
    }
    return data;
  };

  // 便捷入口：A() 立即补扫一次视野内怪物并合并导出全部（传送点+边界走图+怪物刷新点）；A(true) 另下载 JSON
  W.A = function (download) {
    if (W.__mir2spawns && W.__mir2spawns.scan) W.__mir2spawns.scan();
    return W.__dumpAll(download);
  };

  console.log(TAG + ' ready', { teleportHooked: TELEPORT_HOOKED, spawnHooked: SPAWN_HOOKED, currentMap: mapInfo() });
  console.log('[提示] 采集完直接敲 A() 合并导出；A(true) 下载 JSON。');
})();
