'use strict';

let mapIndex = [];                 // [{id, len, kb, name, width, height}]
const mapSelected = new Set();     // selected ids
let mapLoaded = false;
let mapSourceFile = '';

function numOrNull(id) {
    const v = $('#' + id).value.trim();
    if (!v) return 0;
    const n = parseInt(v, 10);
    return isNaN(n) ? 0 : n;
}

async function loadMapData() {
    const btn = $('#btnParseMap');
    btn.disabled = true;
    btn.textContent = '加载中…';
    const datUrl = $('#mapDatPath').value.trim();
    log('开始加载地图数据（按 Config.cs 中的 URL 自动下载）…');
    try {
        const res = await api('POST', '/api/map/load', { datUrl });
        if (!res.ok) { log('加载失败: ' + res.error, 'err'); return; }
        mapIndex = res.entries;
        mapSourceFile = res.sourceFile || '';
        mapLoaded = true;
        mapSelected.clear();
        $('#mapDatPath').value = mapSourceFile;
        setDot('mapDatPath', true);
        $('#mapCountInfo').textContent =
            `共 ${res.count} 张 · 配置 ${res.configCount} 条 · ${(res.totalBytes / 1048576).toFixed(2)} MB`;
        (res.messages || []).forEach(m => log(m, 'ok'));
        renderSourceUrls(res);
        renderMapTable();
        log(`加载完成：${res.count} 张地图，平均 ${(res.totalBytes / res.count / 1024).toFixed(1)} KB/张`, 'ok');
    } catch (e) {
        log('加载异常: ' + e.message, 'err');
    } finally {
        btn.disabled = false;
        btn.textContent = '重新加载地图数据';
    }
}

function renderSourceUrls(res) {
    renderUrlList($('#urlMapConfig'), res.mapConfigUrls);
    renderUrlList($('#urlMapJs'), res.mapJsUrls);
}

function renderMapTable() {
    const tb = $('#mapTbody');
    tb.innerHTML = '';
    const q = $('#mapSearch').value.trim().toLowerCase();
    let n = 0;
    mapIndex.forEach((e, i) => {
        const name = (e.name || '').toString().toLowerCase();
        if (q && !String(e.id).includes(q) && !name.includes(q)) return;
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td><input type="checkbox" class="mc" data-id="${e.id}" ${mapSelected.has(e.id) ? 'checked' : ''}/></td>
            <td>${i + 1}</td>
            <td>${e.id}</td>
            <td class="title-cn">${e.name || '—'}</td>
            <td>${e.kb}</td>
            <td>
                <button class="btn btn-ghost btn-xs act-prev">预览</button>
                <button class="btn btn-ghost btn-xs act-exp">导出</button>
                <button class="btn btn-ghost btn-xs act-rel">相对路径</button>
            </td>`;
        tr.querySelector('.mc').addEventListener('change', ev => {
            if (ev.target.checked) mapSelected.add(e.id); else mapSelected.delete(e.id);
            updateMapSel();
        });
        tr.querySelector('.act-prev').addEventListener('click', ev => { ev.stopPropagation(); previewBoth(e.id); });
        tr.querySelector('.act-exp').addEventListener('click', ev => { ev.stopPropagation(); extractOneNested(e.id); });
        tr.querySelector('.act-rel').addEventListener('click', ev => { ev.stopPropagation(); extractOneRelative(e.id); });
        tr.addEventListener('click', () => previewBoth(e.id));
        tb.appendChild(tr);
        n++;
    });
    $('#mapListWrap').classList.remove('hidden');
    $('#mapSelInfo').textContent = `显示 ${n} 张`;
    updateMapSel();
}

function updateMapSel() {
    const n = mapSelected.size;
    const btn = $('#btnExtract');
    btn.disabled = n === 0;
    btn.textContent = `提取选中(嵌套) (${n})`;
    const btnRel = $('#btnExtractRel');
    if (btnRel) { btnRel.disabled = n === 0; btnRel.textContent = `提取选中(相对路径) (${n})`; }
}

function findEntry(id) { return mapIndex.find(e => e.id === id); }

// 当前预览（用于“下载”按钮保存图片）。redSrc / realSrc 为 dataURL。
let currentPreview = { id: 0, name: '', redSrc: '', realSrc: '', miniSrc: '' };

function safeName(s) {
    const o = (s || '').replace(/[\\/:*?"<>|]/g, '_').trim();
    return o || 'map';
}

// 合并后的“预览”：一次刷新红绿可走性掩码 + 真实瓦片 + 小地图三张图
async function previewBoth(id) {
    $('#previewImg').src = '';
    $('#previewRealImg').src = '';
    $('#previewMiniImg').src = '';
    currentPreview.redSrc = ''; currentPreview.realSrc = ''; currentPreview.miniSrc = '';
    await previewMap(id);
    await previewReal(id);
    await previewMinimap(id);
}

async function previewMap(id) {
    if (!mapSourceFile) { toast('请先加载地图数据'); return; }
    const entry = findEntry(id) || {};
    const ow = numOrNull('mapWidth'), oh = numOrNull('mapHeight');
    try {
        const res = await api('POST', '/api/map/decode', {
            path: '',
            id,
            width: ow || entry.width || 0,
            height: oh || entry.height || 0,
            destination: ''
        });
        if (!res.ok) { log(`预览 ${id} 失败: ` + res.error, 'err'); return; }
        $('#previewEmpty').classList.add('hidden');
        $('#previewBody').classList.remove('hidden');
        $('#previewName').textContent = `地图 ${id}${entry.name ? ' · ' + entry.name : ''}`;
        $('#previewImg').src = res.previewPngBase64;
        currentPreview.id = id;
        currentPreview.name = entry.name || '';
        currentPreview.redSrc = res.previewPngBase64;
        let stats = `网格 ${res.cols} × ${res.rows} ｜ 总格 ${res.cells}（数据块 ${res.rawCells}）｜ 可走 ${res.walkable} ｜ 遮挡 ${res.cover} ｜ 安全区 ${res.safe}`;
        if (res.warn) stats += `\n⚠ ${res.warn}`;
        $('#previewStats').textContent = stats;
    } catch (e) { log('预览异常: ' + e.message, 'err'); }
}

// 预览真实地图：基于已下载的 zipmap 瓦片（<名>_<id>/tiles/）即时拼接缩略 PNG
async function previewReal(id) {
    if (!mapSourceFile) { toast('请先加载地图数据'); return; }
    const dest = $('#mapDest').value.trim();
    if (!dest) { $('#previewRealStats').textContent = '未设置输出目录，无法预览真实图（请先设置输出目录并提取下载瓦片）'; return; }
    const entry = findEntry(id) || {};
    try {
        const res = await api('POST', '/api/map/previewReal', {
            id,
            img: entry.img,
            name: entry.name,
            width: entry.width,
            height: entry.height,
            destination: dest
        });
        if (!res.ok) { $('#previewRealStats').textContent = '真实图预览失败：' + res.error; toast(res.error); return; }
        $('#previewEmpty').classList.add('hidden');
        $('#previewBody').classList.remove('hidden');
        $('#previewName').textContent = `地图 ${id}${entry.name ? ' · ' + entry.name : ''}（真实瓦片）`;
        $('#previewRealImg').src = res.previewPngBase64;
        currentPreview.realSrc = res.previewPngBase64;
        $('#previewRealStats').textContent =
            `拼接 ${res.tiles} 张瓦片 ｜ 网格 ${res.cols}×${res.rows}（缩略预览，全分辨率见 <名>_<id>/tiles/）`;
    } catch (e) { log('预览真实地图异常: ' + e.message, 'err'); }
}

// 预览小地图：优先取 minimap/{img}.jpg 独立整图，缺则回退 minimaps 图集 {img}_png 帧（再回退 43005_png）
async function previewMinimap(id) {
    if (!mapSourceFile) { toast('请先加载地图数据'); return; }
    const entry = findEntry(id) || {};
    try {
        const res = await api('POST', '/api/map/previewMinimap', {
            id,
            img: entry.img,
            name: entry.name
        });
        $('#previewEmpty').classList.add('hidden');
        $('#previewBody').classList.remove('hidden');
        if (!res.ok) {
            $('#previewMiniImg').src = '';
            currentPreview.miniSrc = '';
            $('#previewMiniStats').textContent = '小地图预览失败：' + res.error;
            return;
        }
        $('#previewMiniImg').src = res.previewPngBase64;
        currentPreview.miniSrc = res.previewPngBase64;
        $('#previewMiniStats').textContent = `小地图 ${res.w}×${res.h}（${res.source}）`;
    } catch (e) { log('预览小地图异常: ' + e.message, 'err'); }
}

// 把当前预览的红绿图 / 真实图 / 小地图 保存为本地 PNG 文件。
// 经 fetch→Blob 再下载，避免大尺寸 dataURL 在 <a download> 下静默失败（真实图下载不下来的根因）。
async function downloadPreview(kind) {
    let src, label, ext;
    if (kind === 'red') { src = currentPreview.redSrc; label = '红绿可走性图'; ext = '_walk.png'; }
    else if (kind === 'real') { src = currentPreview.realSrc; label = '真实地图图'; ext = '_real.png'; }
    else { src = currentPreview.miniSrc; label = '小地图'; ext = '_minimap.png'; }
    if (!src) { toast('请先点"预览"生成' + label); return; }
    try {
        const blob = await (await fetch(src)).blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = safeName(currentPreview.name) + '_' + currentPreview.id + ext;
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        log(`已下载${label}：${a.download}`, 'ok');
    } catch (e) { log('下载失败: ' + e.message, 'err'); }
}

// ---------------- 提取选中地图资源（嵌套）：下载真实瓦片 + 解析 JSON + 提取小地图 ----------------
async function extractMapsWith(maps, btnEl) {
    if (!mapSourceFile) { toast('请先加载地图数据'); return; }
    const dest = $('#mapDest').value.trim();
    if (!dest) { toast('请设置输出目录'); return; }
    if (!maps.length) { toast('请选择地图'); return; }
    const ids = maps.map(m => m.id);
    if (btnEl) btnEl.disabled = true;
    try {
        // ① 下载真实地图瓦片（zipmap）并（可选）拼接总览图
        const r1 = await api('POST', '/api/map/downloadTiles', { Maps: maps, Destination: dest, Stitch: $('#chkStitch').checked });
        if (!r1.ok) { log('启动失败: ' + r1.error, 'err'); setMapStatus('失败', 'err'); return; }
        log(`开始提取（嵌套，${r1.totalMaps} 张）：① 下载真实瓦片 → ② 解析 JSON → ③ 提取小地图`, 'ok');
        await pollTiles(r1.jobId);
        // ② 解析地图数据为 JSON（不再单独写红绿 PNG，预览用“预览”按钮）
        setMapStatus('解析 JSON…', 'busy');
        const r2 = await api('POST', '/api/map/export', { path: '', ids, destination: dest, writePng: false });
        if (!r2.ok) { log('解析 JSON 失败: ' + r2.error, 'err'); setMapStatus('JSON 失败', 'err'); return; }
        // ③ 从 minimaps.png 图集按 img 裁出每幅地图的小地图
        setMapStatus('提取小地图…', 'busy');
        let minMsg = '';
        try {
            const r3 = await api('POST', '/api/map/minimap', { Maps: maps, Destination: dest });
            if (r3.ok) {
                const skipped = (r3.results || []).filter(x => !x.ok);
                minMsg = `｜小地图 ${r3.extracted}/${r3.total}`;
                if (skipped.length) log(`  小地图跳过 ${skipped.length} 张（无小地图帧）`, 'ok');
            }
        } catch (e) { log('提取小地图失败: ' + e.message, 'err'); }
        log(`已完成：每张地图一个「地图名_id」文件夹，内含 tiles/ + map_<id>_real.png + map_<id>.json + minimap.png`, 'ok');
        setMapStatus(`已完成：${r2.exported} 张（真实瓦片 + JSON${minMsg}）`, 'ok');
        toast(`提取完成：${r2.exported} 张（真实瓦片 + JSON + 小地图）`);
    } catch (e) { log('异常: ' + e.message, 'err'); setMapStatus('异常', 'err'); }
    finally { if (btnEl) btnEl.disabled = false; updateMapSel(); }
}

async function extractMaps() {
    const maps = mapIndex.filter(e => mapSelected.has(e.id))
        .map(e => ({ id: e.id, img: e.img, name: e.name, width: e.width, height: e.height }));
    await extractMapsWith(maps, $('#btnExtract'));
}

async function extractOneNested(id) {
    const e = findEntry(id);
    if (!e) { toast('找不到该地图'); return; }
    await extractMapsWith([{ id: e.id, img: e.img, name: e.name, width: e.width, height: e.height }], null);
}

// ---------------- 提取地图资源（相对路径平铺，与「提取图集」同风格） ----------------
async function extractRelativeWith(list, btnEl) {
    const dest = $('#mapDest').value.trim();
    if (!dest) { toast('请设置输出目录'); return; }
    if (btnEl) btnEl.disabled = true;
    const N = list.length;
    setMapStatus('提取中…', 'busy');
    setTileProgress(0, `准备相对路径提取（共 ${N} 张）`);
    try {
        let done = 0;
        for (const e of list) {
            done++;
            setTileProgress(Math.round((done - 1) / N * 100), `正在提取 第 ${done} / ${N} 张：地图 ${e.id}（${e.name || ''}）`);
            log(`相对路径提取 地图 ${e.id}（${e.name || ''}）…`);
            const res = await api('POST', '/api/map/resources', {
                mapId: e.id,
                outDir: dest,
                img: e.img ? parseInt(e.img, 10) : 0,
                width: e.width || 0,
                height: e.height || 0
            });
            if (!res.ok) { log(`  地图 ${e.id} 失败: ` + res.error, 'err'); }
            else { log(`  地图 ${e.id}：瓦片 下载 ${res.tilesDownloaded} 跳过 ${res.tilesSkipped} 失败 ${res.tilesFailed}；小地图 ${res.minimapOk ? '有(' + res.minimapLocal + ')' : '无'}；逻辑数据 [${res.dataFiles.join(', ')}]`, 'ok'); }
            setTileProgress(Math.round(done / N * 100), `已完成 ${done} / ${N} 张：地图 ${e.id}（${e.name || ''}）`);
        }
        setMapStatus(`相对路径提取完成（${N} 张）`, 'ok');
        toast(`相对路径提取完成（${N} 张）`);
    } catch (e) { log('异常: ' + e.message, 'err'); setMapStatus('异常', 'err'); }
    finally { if (btnEl) btnEl.disabled = false; updateMapSel(); }
}

async function extractMapsRelative() {
    const list = mapIndex.filter(e => mapSelected.has(e.id))
        .map(e => ({ id: e.id, img: e.img, name: e.name, width: e.width, height: e.height }));
    if (!list.length) { toast('请选择地图'); return; }
    await extractRelativeWith(list, $('#btnExtractRel'));
}

async function extractOneRelative(id) {
    const e = findEntry(id);
    if (!e) { toast('找不到该地图'); return; }
    await extractRelativeWith([{ id: e.id, img: e.img, name: e.name, width: e.width, height: e.height }], null);
}

function pollTiles(jobId) {
    return new Promise(resolve => {
        const timer = setInterval(async () => {
            try {
                const p = await api('GET', `/api/map/tilesProgress?job=${jobId}`);
                if (!p || p.status === 'error') {
                    clearInterval(timer);
                    setTileProgress(100, '下载异常');
                    log('真实地图瓦片下载异常', 'err');
                    resolve(); return;
                }
                const pct = p.percent || 0;
                setTileProgress(pct, `${pct}%  地图 ${p.doneMaps}/${p.totalMaps}  瓦片 ${p.doneTiles}/${p.totalTiles}  失败 ${p.failed} 缺失 ${p.missing}`);
                if (p.status === 'done') {
                    clearInterval(timer);
                    setTileProgress(100, `完成：瓦片 ${p.doneTiles} 失败 ${p.failed} 缺失 ${p.missing}`);
                    toast('真实地图瓦片下载完成');
                    (p.messages || []).forEach(m => log(m, 'ok'));
                    resolve();
                }
            } catch (_) { /* 继续轮询 */ }
        }, 400);
    });
}

// ---------------- 地图页初始化 ----------------
$('#btnParseMap').addEventListener('click', loadMapData);
$('#mapSearch').addEventListener('input', renderMapTable);
$('#btnMapSelAll').addEventListener('click', () => { mapIndex.forEach(e => mapSelected.add(e.id)); renderMapTable(); });
$('#btnMapClearAll').addEventListener('click', () => { mapSelected.clear(); renderMapTable(); });
$('#mapChkAll').addEventListener('change', ev => {
    if (ev.target.checked) mapIndex.forEach(e => mapSelected.add(e.id)); else mapSelected.clear();
    renderMapTable();
});
$('#btnExtract').addEventListener('click', extractMaps);
$('#btnDownloadRed').addEventListener('click', () => downloadPreview('red'));
$('#btnDownloadReal').addEventListener('click', () => downloadPreview('real'));
$('#btnDownloadMini').addEventListener('click', () => downloadPreview('mini'));
$('#btnExportCsv').addEventListener('click', () => {
    if (!mapIndex.length) { toast('请先加载地图数据'); return; }
    let csv = 'id,name,len_bytes,kb,width,height\n';
    mapIndex.forEach(e => csv += `${e.id},"${e.name || ''}",${e.len},${e.kb},${e.width},${e.height}\n`);
    const blob = new Blob([csv], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'map_index.csv';
    a.click();
    log('已导出索引 CSV（含地图名，' + mapIndex.length + ' 行）', 'ok');
});

// ---------------- 地图页初始化 ----------------
$('#btnExtractRel').addEventListener('click', extractMapsRelative);

// 进入页面即自动加载地图数据（与旧版“点总览卡片进地图页即加载”一致）
loadMapData();
