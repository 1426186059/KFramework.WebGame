/* MIR2 运行时怪物数据 dump —— 在游戏 frame 的 DevTools Console 粘贴执行
 *
 * 注意：67001 顶层含 exitIdList，是「视野进出增量同步」包，addMonsters 的 x/y 是
 *       实体当前位置。按 (mid,x,y) 聚合会把怪物移动轨迹当成刷怪点，且每张图只留前
 *       300 个点。要生成 Respawns 请用 mir2-capture-spawns.js（hook emIns.createEntity）。
 *       本脚本保留用于：核对运行时对象、观察协议号分布、看怪物实体字段。
 *
 * 前提结论（已核实）：线上 10.25487.1 的 0~5config 共 371 张静态表，
 * 加上 cm 运行时注入的 38 个 key 共 409 个，里面没有任何 mapmonster / mapxxx 刷怪表。
 *   posJson  = ResUrl.url("pos", 17) → UIModel 目录下的 UI 模块占位表；
 *   mapDatas = map<resVersion>.dat 解压出的 4-bit/格 地形标志（墙/遮挡/安全区）；
 *   mapMonsterNum/Time = 协议 108004/108005 的运行时字典，只有"某地图剩余怪数"。
 * 所以刷怪实体只能来自服务端协议，落在 emIns._monsterDic 里。
 * 直接读内存 = 截获服务端刷怪结果，比抓 WebSocket 帧更完整（含怪物 id 与坐标）。
 *
 * 用法：
 *   1) 进游戏（任意地图）后粘贴本脚本 → 打印 [MIR2-MON] 当前地图快照并复制到剪贴板；
 *   2) 切一次地图（67001 只在进场景时下发）→ 自动记入 frames；
 *      再执行 __mir2mon.snap() 采该地图的实体分布；
 *   3) 执行 __mir2mon.exportCsv() 把累计的 67001 帧聚合成刷怪点 CSV，贴回即可；
 *      __mir2mon.exportJson() 看带地图宽高的明细，__mir2mon.dump() 导全量。
 *   注意：粘贴脚本之前下发的 67001 抓不到，必须切图/重登才会再发一次；
 *         但 _monsterDic 快照随时可读，不依赖切图。
 *         重新粘贴本脚本不会丢历史，也不会重复挂钩子。
 */
(function () {
  var W = window;
  var prev = W.__mir2mon;
  var HIST = prev && prev.snapHistory ? prev.snapHistory : [];
  var NOTIFS = prev && prev.notifs ? prev.notifs : [];
  var FRAMES = prev && prev.frames ? prev.frames : [];

  function keys(o) { try { return Object.keys(o); } catch (e) { return []; } }
  function val(v) {
    if (v === null || v === undefined) return v;
    var t = typeof v;
    if (t === 'number' || t === 'boolean') return v;
    if (t === 'string') return v.slice(0, 60);
    if (t === 'function') return '[fn]';
    if (Array.isArray(v)) return '[array ' + v.length + ']';
    return '[object]';
  }
  function shallow(o, n) {
    var r = {};
    keys(o).slice(0, n || 45).forEach(function (k) {
      try { r[k] = val(o[k]); } catch (e) {}
    });
    return r;
  }
  // 兼容 plain object 与 egret Dictionary（内部键在 .map / ._dic / .keys）
  function dictKeys(d) {
    if (!d) return [];
    var ks = keys(d);
    if (ks.length <= 3) {
      ['keys', 'map', '_dic', 'dic', 'items'].forEach(function (p) {
        try {
          var alt = d[p];
          if (!alt) return;
          var k2 = Array.isArray(alt) ? alt.map(String) : keys(alt);
          if (k2.length > ks.length) ks = k2;
        } catch (e) {}
      });
    }
    return ks;
  }
  function dictGet(d, k) {
    try {
      if (d[k] && typeof d[k] === 'object') return d[k];
      var alt = d.map || d._dic || d.dic;
      if (alt && alt[k]) return alt[k];
      return d[k];
    } catch (e) { return null; }
  }
  function readDict(d) {
    var ks = dictKeys(d);
    var samples = ks.slice(0, 4).map(function (k) {
      return { key: String(k), fields: shallow(dictGet(d, k), 50) };
    });
    var agg = {};
    ks.forEach(function (k) {
      var e = dictGet(d, k);
      if (!e || typeof e !== 'object') return;
      var id = e.monid !== undefined ? e.monid :
               e.monId !== undefined ? e.monId :
               e.configId !== undefined ? e.configId :
               e.cfgId !== undefined ? e.cfgId :
               e.model !== undefined ? e.model :
               e.baseId !== undefined ? e.baseId : e.id;
      var key = String(id === undefined ? '?' : id);
      if (!agg[key]) agg[key] = { id: key, count: 0, xy: [] };
      agg[key].count++;
      if (agg[key].xy.length < 6) {
        var pt = [e.x, e.y, e.gridX, e.gridY, e.cellX, e.cellY, e.posX, e.posY]
          .filter(function (v) { return typeof v === 'number'; });
        if (pt.length) agg[key].xy.push(pt.slice(0, 4));
      }
    });
    return {
      count: ks.length,
      samples: samples,
      byId: Object.keys(agg).map(function (k) { return agg[k]; }).slice(0, 120)
    };
  }

  // gd.map 的真实字段是 .config + .curMapId，不是 .mapConfig（之前猜错导致 mapId=null）。
  // 实测：切图后 curMapId=103，config 是 cm.map[103] 的配置对象。
  function currentMapInfo() {
    var gm = (W.gd && W.gd.map) || {};
    var cfg = gm.config || gm.mapConfig || gm._mapConfig || gm.cfg || {};
    var id = cfg.id;
    if (id === undefined || id === null) {
      ['curMapId', 'prepareMapid', 'mapId', 'mapID', '_mapId', 'mapid', 'ctostageid'].forEach(function (k) {
        if ((id === undefined || id === null) && gm[k] !== undefined && gm[k] !== null) id = gm[k];
      });
    }
    return { id: (id === undefined ? null : id), name: cfg.name || null, config: cfg, obj: gm };
  }
  function tallyCmds() {
    var m = {};
    NOTIFS.forEach(function (n) { var k = String(n.cmd); m[k] = (m[k] || 0) + 1; });
    return m;
  }

  // 怪物实体真实字段（逆向自 createMonsterByBean / addMonster）：
  //   Monster 实体把配置对象挂在 ._monsterObject / .gameObject 上，
  //   其中 monsterid = cm.monster 的 id，bornX/bornY = 出生格坐标，
  //   addMonster 里用 bornX*MAP_GRID_WIDTH 转像素，所以坐标是格坐标而非像素；
  //   实体自身的 .x/.y 是像素坐标（NPC/玩家/drop 用的就是像素）。
  function monsterBean(e) {
    var o = (e && (e._monsterObject || e.monsterObject || e.gameObject)) || e || {};
    var cfg = o.config || (e && e.config) || {};
    return {
      mid: o.monsterid !== undefined ? o.monsterid :
           o.monid !== undefined ? o.monid :
           o.configId !== undefined ? o.configId : o.mid,
      name: o.name || cfg.name || null,
      x: o.bornX !== undefined ? o.bornX : (o.gridX !== undefined ? o.gridX : o.x),
      y: o.bornY !== undefined ? o.bornY : (o.gridY !== undefined ? o.gridY : o.y),
      hp: o.truehp !== undefined ? o.truehp : o.hp,
      maxHp: o.maxHp,
      uid: o.uid || o.id,
      entityKeys: keys(e).slice(0, 25)
    };
  }
  function readMonsters(d) {
    var ks = dictKeys(d), samples = [], agg = {};
    ks.forEach(function (k) {
      var e = dictGet(d, k);
      var m = monsterBean(e);
      if (samples.length < 4) samples.push({ key: String(k), bean: m, fields: shallow(e, 40) });
      var key = String(m.mid === undefined ? '?' : m.mid);
      if (!agg[key]) {
        // cm.getMonsterConfig(mid) === cm.monster[mid]，含 name/level/model/reliveDelay 等
        var cfgName = null;
        try {
          var mc = (W.cm && W.cm.monster) ? W.cm.monster[m.mid] : null;
          if (mc) cfgName = mc.name || null;
        } catch (e2) {}
        agg[key] = { mid: key, name: m.name, cfgName: cfgName, count: 0, xy: [] };
      }
      agg[key].count++;
      if (agg[key].xy.length < 12 && typeof m.x === 'number' && typeof m.y === 'number') {
        agg[key].xy.push([m.x, m.y]);
      }
    });
    return {
      count: ks.length,
      samples: samples,
      byMid: Object.keys(agg).map(function (k) { return agg[k]; }).slice(0, 150)
    };
  }

  // 抓服务端通知：sendNotif(cmd, data) 是所有协议分发的总入口。
  // 优先挂 net.MapModel.prototype（一定存在且覆盖继承来的方法），兜底挂全局 ModelBase.prototype。
  var HOOKED = false;
  function record(cmd, data) {
    try {
      // 67001 = net.MapModel 的「进入场景实体快照」，addMonsters 就是服务端下发的刷怪结果：
      //   lid:Long, mid:VarInt(怪物配置id), x/y:VarInt(格坐标), ownerLid, ownerName,
      //   dir, hp:Long, maxHp:Long, PerformerBufferBean[], createName
      if (Number(cmd) === 67001 && data && Array.isArray(data.addMonsters)) {
        var mons = data.addMonsters.filter(Boolean).map(function (b) {
          return {
            mid: b.mid, x: b.x, y: b.y, createName: b.createName,
            hp: b.hp && b.hp.toNumber ? b.hp.toNumber() : b.hp,
            maxHp: b.maxHp && b.maxHp.toNumber ? b.maxHp.toNumber() : b.maxHp
          };
        });
        var npcs = (data.addNpcs || []).filter(Boolean).map(function (b) {
          return { nid: b.nid, mapNpcId: b.mapNpcId, x: b.x, y: b.y };
        });
        var mi = currentMapInfo();
        FRAMES.push({
          time: new Date().toISOString(),
          // 优先用包内自带的地图 id；没有再退回运行时 curMapId
          mapId: data.mapId !== undefined ? data.mapId :
                 data.mapid !== undefined ? data.mapid :
                 data.stageId !== undefined ? data.stageId :
                 data.stageid !== undefined ? data.stageid : mi.id,
          mapName: mi.name,
          monsterCount: mons.length,
          monsters: mons.slice(0, 400),
          npcs: npcs.slice(0, 150),
          mapCfgKeys: keys(data).slice(0, 30)
        });
      }
      if (NOTIFS.length < 500) NOTIFS.push({ cmd: cmd, data: shallow(data, 20) });
    } catch (e) {}
  }
  // 重新粘贴脚本时必须重挂钩子，否则 p.sendNotif 仍是旧闭包里的旧 record
  // （旧版读 gd.map.mapConfig → mapId 全为 null）。先恢复原方法避免闭包叠加。
  function hookSendNotif(p) {
    try {
      if (!p || typeof p.sendNotif !== 'function') return false;
      var orig = p.__mir2sendNotifOrig || p.sendNotif;
      p.__mir2sendNotifOrig = orig;
      p.sendNotif = function (cmd, data) {
        record(cmd, data);
        return orig.apply(this, arguments);
      };
      return true;
    } catch (e) { return false; }
  }
  try {
    var proto = (W.net && W.net.MapModel && W.net.MapModel.prototype) ||
                (W.ModelBase && W.ModelBase.prototype);
    HOOKED = hookSendNotif(proto);
  } catch (e) {}

  function snap() {
    var gd = W.gd || {}, em = W.emIns || {}, cm = W.cm || {}, net = W.net || {};
    var gmap = gd.map || {};
    var mi = currentMapInfo();
    var mapId = mi.id;
    return {
      time: new Date().toISOString(),
      mapId: mapId,
      mapName: mi.name,
      mapObj: shallow(gmap, 60),
      mapConfig: shallow(mi.config, 40),
      cmMapEntry: (cm.map && mapId !== null) ? shallow(cm.map[mapId] || {}, 40) : null,
      monsters: readMonsters(em._monsterDic),
      npcs: readDict(em._npcDic),
      players: readDict(em._playerDic),
      drops: readDict(em._dropDic),
      xuanShang: {
        mapMonsterNum: shallow(gd.xuanShang && gd.xuanShang.mapMonsterNum, 60),
        mapMonsterTime: shallow(gd.xuanShang && gd.xuanShang.mapMonsterTime, 60),
        mapMonster1Time: shallow(gd.xuanShang && gd.xuanShang.mapMonster1Time, 60)
      },
      posJsonKeys: keys(cm.posJson || {}).slice(0, 30),
      mapDatasCount: keys(cm.mapDatas || {}).length,
      netProbe: {
        hasModelBase: !!(W.ModelBase || net.ModelBase),
        sendNotifHooked: HOOKED,
        netKeys: keys(net).filter(function (k) {
          return /socket|connect|modelbase|base|manager/i.test(k);
        }).slice(0, 30),
        hasEgretWS: !!(W.egret && W.egret.WebSocket),
        mtwGameKeys: (W.MtwGame && W.MtwGame.Instance) ? keys(W.MtwGame.Instance).slice(0, 40) : []
      },
      notifCount: NOTIFS.length,
      framesCount: FRAMES.length,
      cmdTally: tallyCmds()
    };
  }

  // 把 67001 的高频增量帧聚合成刷怪点：
  //   同帧内按 (mid,x,y) 去重；跨帧累计 seen 次数（seen 越高说明该点越稳定）。
  //   怪物名/等级/复活延迟从 cm.monster[mid] 补齐，地图名/宽高从 cm.map[mapId] 补齐。
  //   reliveDelay 是毫秒，正好可作为 Respawns 的刷新间隔。
  function exportRespawns() {
    var maps = {}, cm = W.cm || {};
    FRAMES.forEach(function (f) {
      var id = String(f.mapId === null || f.mapId === undefined ? '?' : f.mapId);
      if (!maps[id]) maps[id] = { mapName: f.mapName, frames: 0, spawns: {} };
      var M = maps[id];
      M.frames++;
      if (f.mapName && !M.mapName) M.mapName = f.mapName;
      var inFrame = {};
      (f.monsters || []).forEach(function (m) {
        if (!m || m.mid === undefined || m.x === undefined || m.y === undefined) return;
        var k = m.mid + '#' + m.x + '#' + m.y;
        if (inFrame[k]) return;
        inFrame[k] = 1;
        if (!M.spawns[k]) {
          var mc = (cm.monster && cm.monster[m.mid]) || {};
          M.spawns[k] = {
            mid: m.mid,
            name: mc.name || m.createName || '',
            x: m.x, y: m.y, seen: 0,
            reliveDelayMs: mc.reliveDelay === undefined ? null : mc.reliveDelay,
            level: mc.level === undefined ? null : mc.level,
            type: mc.type === undefined ? null : mc.type
          };
        }
        M.spawns[k].seen++;
      });
    });
    var out = [];
    var csv = ['mapId,mapName,mid,monsterName,x,y,seen,reliveDelayMs,level,type'];
    function clean(s) { return String(s === null || s === undefined ? '' : s).replace(/[,\r\n]/g, ' '); }
    Object.keys(maps).forEach(function (id) {
      var M = maps[id];
      var mc = (cm.map && cm.map[id]) || {};
      var name = M.mapName || mc.name || '';
      var spawns = Object.keys(M.spawns).map(function (k) { return M.spawns[k]; })
        .sort(function (a, b) { return b.seen - a.seen; }).slice(0, 300);
      out.push({
        mapId: id, mapName: name,
        width: mc.width === undefined ? null : mc.width,
        height: mc.height === undefined ? null : mc.height,
        frames: M.frames, uniqueSpawnPoints: Object.keys(M.spawns).length,
        spawns: spawns
      });
      spawns.forEach(function (s) {
        csv.push([id, clean(name), s.mid, clean(s.name), s.x, s.y,
                  s.seen, s.reliveDelayMs, s.level, s.type].join(','));
      });
    });
    out.sort(function (a, b) { return b.frames - a.frames; });
    return {
      totalFrames: FRAMES.length,
      notifCount: NOTIFS.length,
      cmdTally: tallyCmds(),
      sampleMapCfgKeys: FRAMES.length ? FRAMES[0].mapCfgKeys : [],
      mapCount: out.length,
      maps: out.slice(0, 60),
      csv: csv.join('\n')
    };
  }

  function emit(o) {
    var text = JSON.stringify(o, null, 2);
    console.log('[MIR2-MON]\n' + text);
    try { copy(text); console.log('[MIR2-MON] 已复制到剪贴板，直接 Ctrl+V 发给助手'); }
    catch (e) {
      try { navigator.clipboard.writeText(text); } catch (e2) {}
    }
  }

  W.__mir2mon = {
    snapHistory: HIST,
    notifs: NOTIFS,
    frames: FRAMES,
    snap: function () { var s = snap(); HIST.push(s); emit(s); return s; },
    exportCsv: function () {
      var r = exportRespawns();
      console.log('[MIR2-RESPAWNS] ' + JSON.stringify({
        totalFrames: r.totalFrames, mapCount: r.mapCount,
        rows: r.csv.split('\n').length - 1,
        truncatedPerMap: 300,
        note: '67001 是视野同步，x/y 含移动轨迹，勿直接当刷怪点',
        cmdTally: r.cmdTally, sampleMapCfgKeys: r.sampleMapCfgKeys
      }) + '\n' + r.csv);
      try { copy(r.csv); console.log('[MIR2-RESPAWNS] CSV 已复制到剪贴板，直接 Ctrl+V 发给助手'); }
      catch (e) {}
      return r;
    },
    exportJson: function () { var r = exportRespawns(); emit(r); return r; },
    dumpFrames: function () {
      emit({
        frameCount: FRAMES.length,
        notifCount: NOTIFS.length,
        cmdTally: tallyCmds(),
        frames: FRAMES
      });
      return FRAMES.length;
    },
    dump: function () {
      emit({ maps: HIST, frames: FRAMES, notifs: NOTIFS.slice(-100) });
      return { maps: HIST.length, frames: FRAMES.length };
    }
  };
  var first = snap();
  HIST.push(first);
  emit(first);
})();
