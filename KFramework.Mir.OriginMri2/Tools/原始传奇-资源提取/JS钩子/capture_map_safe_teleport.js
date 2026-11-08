// ============================================================================
// 原始传奇(JS版) —— 传送点 捕获脚本（含 传送点/边界 自动判别）
// 用法：在【原始传奇】游戏网页按 F12 打开控制台，粘贴本脚本回车运行。
//
// 前提（已逆向确认）：
//   原始传奇客户端/配置/地图数据里没有 door/teleport 静态清单（JS 中 door/传送点 关键字 0 命中），
//   传送点由服务端决定。因此：
//     · 传送点 —— 运行时钩取：在玩家“准备进入某地图”的瞬间，记下
//                 当前地图名 + 玩家所在格子坐标(x,y) + 目标地图，
//                 并按“是否处于地图边缘”自动判别 传送点 / 边界走图，边玩边抓。
//
// 注：安全区已改为从原始地图 .dat 提取（见 MapInfoEditor 全局设置“修正所有地图安全区”），本脚本不再采集。
//
// 判别规则：用 map.config.width/height 反推网格行列(GRID 48×32)，若 (x,y) 落在
//   距边缘 <= __TP_EDGE_MARGIN(默认2) 格内 → 边界走图；否则 → 传送点。
// ============================================================================
(function () {
  const gd = window.gd;
  const map = gd && gd.map;
  if (!map) { console.error('[capture] 未找到 gd.map，请确认在原始传奇游戏页面控制台运行'); return; }

  // 每次运行先清空（避免旧版本/多次粘贴造成的格式混排）；并防止重复钩取
  window.__teleportLog = [];
  window.__TP_EDGE_MARGIN = window.__TP_EDGE_MARGIN || 2; // 边缘容差（格），可调

  // 注：安全区改为从原始地图 .dat 提取（见 MapInfoEditor 全局设置“修正所有地图安全区”），本脚本不再采集。

  // ---------- 网格行列（用于边缘判别）----------
  function gridDims() {
    const c = map.config || {};
    if (!c.width || !c.height) return null;
    const GW = 48, GH = 32; // GameDefine.MAP_GRID_WIDTH/HEIGHT
    return { cols: Math.ceil(c.width / GW), rows: Math.ceil(c.height / GH) };
  }

  // ---------- 玩家坐标：firstPlayer.fighterObject.gridX/gridY ----------
  function readCoord(obj, name) {
    if (!obj) return null;
    if (obj[name] != null) return obj[name];
    if (obj.CurrentLocation && obj.CurrentLocation[name] != null) return obj.CurrentLocation[name];
    return null;
  }
  function playerPos() {
    let p = null;
    try { if (window.emIns && window.emIns.firstPlayer) p = window.emIns.firstPlayer; } catch (e) {}
    if (!p) { try { if (typeof emIns !== 'undefined' && emIns.firstPlayer) p = emIns.firstPlayer; } catch (e) {} }
    if (!p) { try { if (gd.player && gd.player.entity) p = gd.player.entity; } catch (e) {} }
    if (!p) return null;
    const fo = p.fighterObject || p;
    let x = readCoord(fo, 'gridX'); if (x == null) x = readCoord(p, 'gridX');
    let y = readCoord(fo, 'gridY'); if (y == null) y = readCoord(p, 'gridY');
    if (x == null) x = readCoord(fo, 'X'); if (x == null) x = readCoord(p, 'X');
    if (y == null) y = readCoord(fo, 'Y'); if (y == null) y = readCoord(p, 'Y');
    return (x != null && y != null) ? { x, y } : null;
  }

  // ---------- 2) 传送点（钩 prepareToEnterMap，自动判别类型）----------
  function classify(x, y) {
    const d = gridDims();
    if (!d || x == null || y == null) return 'unknown';
    const M = window.__TP_EDGE_MARGIN;
    if (x <= M || y <= M || x >= d.cols - 1 - M || y >= d.rows - 1 - M) return 'border'; // 边界走图
    return 'teleport'; // 地图内部 → 传送点
  }

  if (!window.__tpHooked) {
    const origPTE = map.prepareToEnterMap && map.prepareToEnterMap.bind(map);
    if (origPTE) {
      map.prepareToEnterMap = function (mid) {
        const pos = playerPos();
        const cur = map.config || {};
        const rec = {
          ts: Date.now(),
          sourceMap: (map.curMapId != null ? map.curMapId : cur.id),
          sourceName: cur.name,
          x: pos ? pos.x : null,
          y: pos ? pos.y : null,
          targetMap: mid,
          type: classify(pos ? pos.x : null, pos ? pos.y : null)
        };
        window.__teleportLog.push(rec);
        console.log('[传送点捕获]', JSON.stringify(rec));
        return origPTE(mid);
      };
      window.__tpHooked = true;
      console.log('[capture] 已钩取 gd.map.prepareToEnterMap —— 记录 (源地图,坐标,目标地图) 并自动判别 传送点/边界。');
    } else {
      console.warn('[capture] 未找到 gd.map.prepareToEnterMap，钩取失败；可手动执行 __hookEnter() 改钩 enterMap。');
    }
  } else {
    console.log('[capture] 已钩取过，跳过重复钩取。如需清空记录请用 __clearTP()。');
  }

  console.log('[capture] 在游戏里把各传送点/回城/传送员/边界都踩一遍，记录写入 window.__teleportLog。');
  console.log('[capture] 玩一会儿后执行  __dumpTP()  查看汇总；__dumpTP(true)  自动下载 JSON。');

  window.__clearTP = function () { window.__teleportLog = []; console.log('[capture] 已清空记录'); };

  window.__dumpTP = function (download) {
    // 兼容旧版 tag 格式：mapId→sourceMap, mapName→sourceName
    const norm = r => ({
      sourceMap: r.sourceMap != null ? r.sourceMap : r.mapId,
      sourceName: r.sourceName || r.mapName,
      x: r.x != null ? r.x : null, y: r.y != null ? r.y : null,
      targetMap: r.targetMap,
      type: r.type || 'unknown'
    });
    const pairKey = r => r.sourceMap + '|' + r.targetMap;
    // 同一 (源地图,目标地图) 只保留 1 条，优先带坐标的（去掉切图瞬间 x/y=null 的重复项）
    const best = new Map();
    for (const raw of window.__teleportLog) {
      const r = norm(raw);
      const k = pairKey(r);
      const prev = best.get(k);
      if (!prev) { best.set(k, r); continue; }
      const rHas = r.x != null && r.y != null;
      const pHas = prev.x != null && prev.y != null;
      if (rHas && !pHas) best.set(k, r);          // 新记录带坐标、旧的无 → 替换
      else if (rHas && pHas) best.set(k, prev);   // 都带坐标 → 任取（已相同）
      // 否则（新无坐标）→ 保留原带坐标的
    }
    const teleports = [], borders = [], unknowns = [];
    for (const r of best.values()) {
      if (r.type === 'teleport') teleports.push(r);
      else if (r.type === 'border') borders.push(r);
      else unknowns.push(r);
    }
    const data = {
      传送点: teleports,
      边界走图: borders,
      未判别: unknowns,
      统计: { 传送点: teleports.length, 边界走图: borders.length, 未判别: unknowns.length, 原始记录: window.__teleportLog.length }
    };
    console.log('==== 汇总（传送点 ' + teleports.length + ' / 边界 ' + borders.length + ' / 未判别 ' + unknowns.length + '）====');
    console.log(JSON.stringify(data, null, 2));
    if (download) {
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      const first = window.__teleportLog[0];
      const sid = (first && (first.sourceMap != null ? first.sourceMap : first.mapId)) || '?';
      a.download = `origin_map_${sid}_tp.json`;
      document.body.appendChild(a); a.click(); a.remove();
      console.log('[capture] 已下载 origin_map_' + sid + '_tp.json');
    }
    return data;
  };

  // 备用：若 prepareToEnterMap 没被触发，可手动执行 __hookEnter() 改钩 enterMap
  window.__hookEnter = function () {
    if (window.__tpHooked) { console.log('[capture] 已钩取，跳过'); return; }
    const orig = map.enterMap && map.enterMap.bind(map);
    if (!orig) { console.warn('无 enterMap'); return; }
    map.enterMap = function (mid) {
      const pos = playerPos();
      const cur = map.config || {};
      window.__teleportLog.push({
        ts: Date.now(),
        sourceMap: (map.curMapId != null ? map.curMapId : cur.id),
        sourceName: cur.name, x: pos ? pos.x : null, y: pos ? pos.y : null,
        targetMap: mid, type: classify(pos ? pos.x : null, pos ? pos.y : null)
      });
      return orig(mid);
    };
    window.__tpHooked = true;
    console.log('[capture] 已钩取 gd.map.enterMap（备用）');
  };
})();
