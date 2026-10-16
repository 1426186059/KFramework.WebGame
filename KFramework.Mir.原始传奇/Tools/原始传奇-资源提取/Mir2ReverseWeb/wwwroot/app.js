'use strict';

const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];

let mapIndex = [];                 // [{id, len, kb, name, width, height}]
const mapSelected = new Set();     // selected ids
let mapLoaded = false;
let mapSourceFile = '';

// ---------------- 基础工具 ----------------
async function api(method, url, body) {
    const opt = { method, headers: {} };
    if (body !== undefined) {
        opt.headers['Content-Type'] = 'application/json';
        opt.body = JSON.stringify(body);
    }
    const r = await fetch(url, opt);
    let data = null;
    try { data = await r.json(); } catch (_) {}
    if (!r.ok) throw new Error((data && (data.error || data.Error)) || r.statusText);
    return data;
}

function log(msg, cls) {
    const box = $('#log');
    const line = document.createElement('div');
    if (cls) line.className = cls;
    const t = new Date().toLocaleTimeString();
    line.textContent = `[${t}] ${msg}`;
    box.appendChild(line);
    box.scrollTop = box.scrollHeight;
}

function toast(msg) {
    const el = $('#toast');
    el.textContent = msg;
    el.classList.remove('hidden');
    clearTimeout(toast._t);
    toast._t = setTimeout(() => el.classList.add('hidden'), 2200);
}

function setDot(id, ok) {
    const d = $('#dot-' + id);
    if (d) d.className = 'dot ' + (ok ? 'ok' : 'bad');
}

function fmtSize(n) {
    if (n >= 1048576) return (n / 1048576).toFixed(2) + ' MB';
    if (n >= 1024) return (n / 1024).toFixed(1) + ' KB';
    return n + ' B';
}

// 图集下载状态：busy 时显示动画进度条；ok/err 时显示结果文字
function setDlStatus(text, cls) {
    const el = $('#dlStatus');
    if (el) { el.textContent = text; el.className = 'dl-status ' + (cls || ''); }
    const w = $('#dlProgressWrap'), bar = $('#dlProgressBar');
    if (w && bar) {
        const busy = cls === 'busy';
        w.classList.toggle('hidden', !busy);
        bar.classList.toggle('indeterminate', busy);
    }
}

// 地图提取状态（复用同一套进度条样式）
function setMapStatus(text, cls) {
    const el = $('#exportInfo');
    if (el) { el.textContent = text; el.className = 'dl-status ' + (cls || ''); }
    const w = $('#mapProgressWrap'), bar = $('#mapProgressBar');
    if (w && bar) {
        const busy = cls === 'busy';
        w.classList.toggle('hidden', !busy);
        bar.classList.toggle('indeterminate', busy);
    }
}

// 真实百分比进度（真实地图瓦片下载）
function setTileProgress(pct, text) {
    const w = $('#mapProgressWrap'), bar = $('#mapProgressBar');
    if (w) w.classList.remove('hidden');
    if (bar) { bar.classList.remove('indeterminate'); bar.style.width = Math.max(0, Math.min(100, pct)) + '%'; }
    const el = $('#exportInfo');
    if (el) { el.textContent = text; el.className = 'dl-status'; }
}

// ---------------- 视图切换 ----------------
function switchView(v) {
    $$('.view').forEach(s => s.classList.toggle('hidden', s.id !== 'view-' + v));
    const back = $('#btnBack');
    if (back) back.classList.toggle('hidden', v === 'overview'); // 总览页不显示返回
    const layout = $('.layout');
    if (layout) layout.classList.toggle('overview', v === 'overview'); // 总览页隐藏左右栏，更干净
}

// 返回按钮：回到总览页
$('#btnBack').addEventListener('click', () => switchView('overview'));

// 总览页：点卡片跳到对应资源页
$$('.ov-card').forEach(c => c.addEventListener('click', () => {
    const v = c.dataset.go;
    switchView(v);
    if (v === 'map' && !mapLoaded) loadMapData();
}));

// ---------------- 浏览弹窗 ----------------
const browse = { target: '', mode: 'dir', filter: '', path: '' };

async function openBrowse(targetId, mode, filter) {
    browse.target = targetId;
    browse.mode = mode;
    browse.filter = filter || '';
    browse.path = $('#' + targetId).value || (navigator.platform.indexOf('Win') >= 0 ? 'C:\\' : '/');
    $('#browseTitle').textContent = mode === 'file' ? '选择文件' : '选择目录';
    $('#browsePick').classList.toggle('hidden', mode !== 'dir');
    $('#browseModal').classList.remove('hidden');
    await browseRender();
}
async function browseRender() {
    $('#browsePath').textContent = browse.path;
    let list;
    try {
        list = await api('GET', `/api/fs?path=${encodeURIComponent(browse.path)}&files=${browse.mode === 'file'}&filter=${encodeURIComponent(browse.filter)}`);
    } catch (e) { log('浏览失败: ' + e.message, 'err'); return; }
    const box = $('#browseList');
    box.innerHTML = '';
    list.forEach(it => {
        const div = document.createElement('div');
        div.className = 'browse-item';
        div.innerHTML = `<span class="ic">${it.isDir ? '📁' : '📄'}</span><span>${it.name}</span>`;
        div.addEventListener('click', () => {
            if (it.isDir) { browse.path = it.path; browseRender(); }
            else if (browse.mode === 'file') { pickPath(it.path); }
        });
        box.appendChild(div);
    });
}
function pickPath(p) {
    $('#' + browse.target).value = p;
    $('#browseModal').classList.add('hidden');
    log('已选择: ' + p);
}
$('#browseClose').addEventListener('click', () => $('#browseModal').classList.add('hidden'));
$('#browseUp').addEventListener('click', () => {
    const p = browse.path;
    const up = p.replace(/[\\/][^\\/]+$/, '');
    if (up && up !== browse.path) { browse.path = up; browseRender(); }
});
$('#browsePick').addEventListener('click', () => pickPath(browse.path));

$$('[data-browse]').forEach(b => b.addEventListener('click', () => openBrowse(b.dataset.browse, 'dir', '')));
$$('[data-browse-file]').forEach(b => b.addEventListener('click', () => openBrowse(b.dataset.browseFile, 'file', b.dataset.filter || '')));

// ---------------- 提取地图资源（自动下载 + 解析） ----------------
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

function renderUrlList(el, urls) {
    el.innerHTML = '';
    (urls || []).forEach(u => {
        const li = document.createElement('li');
        const a = document.createElement('a');
        a.href = u; a.target = '_blank'; a.rel = 'noopener'; a.textContent = u;
        li.appendChild(a);
        el.appendChild(li);
    });
    if (!(urls && urls.length)) el.innerHTML = '<li class="muted">（无）</li>';
}

function renderSourceUrls(res) {
    renderUrlList($('#urlMapConfig'), res.mapConfigUrls);
    renderUrlList($('#urlMapJs'), res.mapJsUrls);
}

$('#btnParseMap').addEventListener('click', loadMapData);

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
            <td><button class="btn btn-ghost btn-xs act-prev">预览</button></td>`;
        tr.querySelector('.mc').addEventListener('change', ev => {
            if (ev.target.checked) mapSelected.add(e.id); else mapSelected.delete(e.id);
            updateMapSel();
        });
        tr.querySelector('.act-prev').addEventListener('click', ev => { ev.stopPropagation(); previewBoth(e.id); });
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
    btn.textContent = `提取选中地图资源 (${n})`;
}

$('#mapSearch').addEventListener('input', renderMapTable);
$('#btnMapSelAll').addEventListener('click', () => { mapIndex.forEach(e => mapSelected.add(e.id)); renderMapTable(); });
$('#btnMapClearAll').addEventListener('click', () => { mapSelected.clear(); renderMapTable(); });
$('#mapChkAll').addEventListener('change', ev => {
    if (ev.target.checked) mapIndex.forEach(e => mapSelected.add(e.id)); else mapSelected.clear();
    renderMapTable();
});

function findEntry(id) { return mapIndex.find(e => e.id === id); }

// 当前预览（用于"下载"按钮保存图片）。redSrc / realSrc 为 dataURL。
let currentPreview = { id: 0, name: '', redSrc: '', realSrc: '', miniSrc: '' };

function safeName(s) {
    const o = (s || '').replace(/[\\/:*?"<>|]/g, '_').trim();
    return o || 'map';
}

// 合并后的"预览"：一次刷新红绿可走性掩码 + 真实瓦片 + 小地图三张图
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

// ---------------- 提取选中地图资源：下载真实瓦片 + 解析 JSON + 提取小地图 ----------------
$('#btnExtract').addEventListener('click', extractMaps);
$('#btnDownloadRed').addEventListener('click', () => downloadPreview('red'));
$('#btnDownloadReal').addEventListener('click', () => downloadPreview('real'));
$('#btnDownloadMini').addEventListener('click', () => downloadPreview('mini'));

async function extractMaps() {
    if (!mapSourceFile) { toast('请先加载地图数据'); return; }
    const dest = $('#mapDest').value.trim();
    if (!dest) { toast('请设置输出目录'); return; }
    const maps = mapIndex.filter(e => mapSelected.has(e.id))
        .map(e => ({ id: e.id, img: e.img, name: e.name, width: e.width, height: e.height }));
    if (!maps.length) { toast('请选择地图'); return; }
    const ids = maps.map(m => m.id);
    const btn = $('#btnExtract');
    btn.disabled = true;
    try {
        // ① 下载真实地图瓦片（zipmap）并（可选）拼接总览图
        const r1 = await api('POST', '/api/map/downloadTiles', { Maps: maps, Destination: dest, Stitch: $('#chkStitch').checked });
        if (!r1.ok) { log('启动失败: ' + r1.error, 'err'); setMapStatus('失败', 'err'); return; }
        log(`开始提取选中地图资源（${r1.totalMaps} 张）：① 下载真实瓦片 → ② 解析 JSON → ③ 提取小地图`, 'ok');
        await pollTiles(r1.jobId);
        // ② 解析地图数据为 JSON（不再单独写红绿 PNG，预览用"预览"按钮）
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
        log(`已完成：每张地图一个「地图名_id」文件夹，内含 tiles/（真实瓦片）+ map_<id>_real.png（拼接图）+ map_<id>.json（数据）+ minimap.png（小地图）`, 'ok');
        setMapStatus(`已完成：${r2.exported} 张（真实瓦片 + JSON${minMsg}）`, 'ok');
        toast(`提取完成：${r2.exported} 张（真实瓦片 + JSON + 小地图）`);
    } catch (e) { log('异常: ' + e.message, 'err'); setMapStatus('异常', 'err'); }
    finally { btn.disabled = false; updateMapSel(); }
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

// ---------------- 提取所有图集（manifest 模式） ----------------
let manifestEntries = [];

async function loadManifest() {
    const btn = $('#btnLoadManifest');
    btn.disabled = true; btn.textContent = '加载中…';
    try {
        const res = await api('POST', '/api/atlas/manifest', {});
        if (!res.ok) { log('加载清单失败: ' + res.error, 'err'); return; }
        manifestEntries = res.entries;
        renderUrlList($('#urlManifest'), res.manifestUrl ? [res.manifestUrl] : []);
        $('#manifestCount').textContent = res.count;
        $('#manifestInfo').textContent = `共 ${res.count} 条资源`;
        (res.messages || []).forEach(m => log(m, 'ok'));
        renderManifestTable();
        $('#btnDownloadAll').disabled = false;
    } catch (e) { log('清单异常: ' + e.message, 'err'); }
    finally { btn.disabled = false; btn.textContent = '加载清单'; }
}

function renderManifestTable() {
    const q = $('#manifestSearch').value.trim().toLowerCase();
    const tb = $('#manifestTbody');
    tb.innerHTML = '';
    const list = q
        ? manifestEntries.filter(e => e.key.toLowerCase().includes(q) || e.hash.toLowerCase().includes(q))
        : manifestEntries;
    list.forEach(e => {
        const tr = document.createElement('tr');
        tr.innerHTML = `<td class="title-cn" title="${e.key}">${e.key}</td><td>${e.hash}</td><td>${fmtSize(e.size)}</td>
            <td><button class="btn btn-ghost btn-xs act-dl">下载</button></td>`;
        tr.querySelector('.act-dl').addEventListener('click', () => downloadOne(e.key));
        tb.appendChild(tr);
    });
    $('#manifestSelInfo').textContent = `显示 ${list.length} 条`;
}

async function downloadOne(key) {
    setDlStatus('下载中…', 'busy');
    try {
        const res = await api('POST', '/api/atlas/download', { key, outDir: $('#atlasOutDir').value.trim() });
        if (!res.ok) { log(`下载 ${key} 失败: ${res.error}`, 'err'); setDlStatus('下载失败', 'err'); return; }
        if (res.skipped) { log(`已存在(跳过) ${key} -> ${res.localPath}`, 'ok'); setDlStatus('已完成（跳过已存在）', 'ok'); }
        else { log(`已下载 ${key} (${(res.bytes / 1024).toFixed(1)} KB) -> ${res.localPath}`, 'ok'); setDlStatus('下载完成', 'ok'); }
    } catch (e) { log(`下载异常 ${key}: ${e.message}`, 'err'); setDlStatus('下载失败', 'err'); }
}

async function downloadAll() {
    const btn = $('#btnDownloadAll');
    btn.disabled = true; btn.textContent = '下载中…';
    setDlStatus('下载中…', 'busy');
    try {
        const res = await api('POST', '/api/atlas/downloadAll', { outDir: $('#atlasOutDir').value.trim() });
        if (!res.ok) { log('全部下载失败: ' + res.error, 'err'); setDlStatus('下载失败', 'err'); return; }
        (res.messages || []).forEach(m => log(m, 'ok'));
        setDlStatus('下载完成', 'ok');
    } catch (e) { log('全部下载异常: ' + e.message, 'err'); setDlStatus('下载失败', 'err'); }
    finally { btn.disabled = false; btn.textContent = '全部下载'; $('#dlAllInfo').textContent = ''; }
}

$('#btnLoadManifest').addEventListener('click', loadManifest);
$('#btnDownloadAll').addEventListener('click', downloadAll);
$('#manifestSearch').addEventListener('input', renderManifestTable);

async function atlasInfo(path, name) {
    try {
        const res = await api('POST', '/api/atlas/info', { path });
        $('#atlasDetailCard').classList.remove('hidden');
        $('#atlasDetailName').textContent = name;
        $('#atlasDetailMeta').textContent =
            `标题: ${res.title || '-'}\n版本: ${res.version}\n声明图像数: ${res.imageCount}\n成功解析: ${res.parsedImages}` +
            (res.error ? `\n错误: ${res.error}` : '') +
            '\n（像素解码 RLE/调色板后续接入）';
        const tb = $('#atlasImgTbody');
        tb.innerHTML = '';
        res.images.slice(0, 500).forEach(im => {
            const tr = document.createElement('tr');
            tr.innerHTML = `<td>${im.index}</td><td>${im.width}</td><td>${im.height}</td><td>${im.offsetX}</td><td>${im.offsetY}</td>`;
            tb.appendChild(tr);
        });
        if (res.images.length > 500) log(`（仅展示前 500 / ${res.images.length} 张图像）`);
    } catch (e) { log('解析图集异常: ' + e.message, 'err'); }
}



// ---------------- 提取配置表（0config*.json 顶层每个键 = 一张表） ----------------
let configTables = [];                 // [{name, kind, count, fields:[{name,csType,optional}]}]
const configSelected = new Set();      // selected table names

function setConfigStatus(text, cls) {
    const el = $('#configStatus');
    if (el) { el.textContent = text; el.className = 'dl-status ' + (cls || ''); }
}

// 根据 数据服务器地址 + 版本号 实时拼出配置 URL（与游戏 onloadCfgAgain 完全一致）
function updateConfigResolvedUrl() {
    const base = ($('#configBaseUrl').value.trim() || 'https://cdn.yscq-wy.yscq.com/fugu_20201203').replace(/\/$/, '');
    const ver = $('#configVersion').value.trim() || '10.25341.1';
    $('#configResolvedUrl').value = `${base}/0config${ver}.json?v=${ver}`;
}

async function loadConfigTables() {
    const btn = $('#btnLoadConfig');
    btn.disabled = true; btn.textContent = '加载中…';
    const baseUrl = $('#configBaseUrl').value.trim();
    const version = $('#configVersion').value.trim();
    updateConfigResolvedUrl();
    const url = $('#configResolvedUrl').value;
    log('开始加载配置表（按游戏 onloadCfgAgain 拼法下载 ' + url + '）…');
    try {
        const res = await api('POST', '/api/config/load', { baseUrl: baseUrl || null, version: version || null });
        if (!res.ok) { log('加载失败: ' + res.error, 'err'); setConfigStatus('失败', 'err'); $('#configLoadInfo').textContent = '0 张'; return; }
        configTables = res.tables || [];
        $('#configLoadInfo').textContent = `共 ${res.count} 张配置表`;
        (res.urls || []).forEach(u => log('配置 URL: ' + u, 'ok'));
        (res.messages || []).forEach(m => log(m, 'ok'));
        renderConfigTable();
        log(`加载完成：${res.count} 张配置表`, 'ok');
    } catch (e) {
        log('加载异常: ' + e.message, 'err');
    } finally {
        btn.disabled = false; btn.textContent = '重新加载配置表';
    }
}

function renderConfigTable() {
    const tb = $('#configTbody');
    tb.innerHTML = '';
    const q = $('#configSearch').value.trim().toLowerCase();
    let n = 0;
    configTables.forEach(t => {
        if (q && !t.name.toLowerCase().includes(q)) return;
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td><input type="checkbox" class="cc" data-name="${t.name}" ${configSelected.has(t.name) ? 'checked' : ''}/></td>
            <td class="title-cn">${t.name}</td>
            <td>${t.kind}</td>
            <td>${t.count}</td>
            <td>${t.fields ? t.fields.length : 0}</td>
            <td><button class="btn btn-ghost btn-xs act-one">提取</button></td>`;
        tr.querySelector('.cc').addEventListener('change', ev => {
            if (ev.target.checked) configSelected.add(t.name); else configSelected.delete(t.name);
            updateConfigSel();
        });
        tr.querySelector('.act-one').addEventListener('click', () => extractConfig([t.name]));
        tb.appendChild(tr);
        n++;
    });
    $('#configListWrap').classList.remove('hidden');
    $('#configSelInfo').textContent = `显示 ${n} 张`;
    updateConfigSel();
}

function updateConfigSel() {
    const n = configSelected.size;
    const btn = $('#btnExtractConfigSel');
    btn.disabled = n === 0;
    btn.textContent = `提取选中配置表 (${n})`;
}

async function extractConfig(names) {
    const outDir = $('#configOutDir').value.trim();
    if (!configTables.length) { toast('请先加载配置表'); return; }
    if ((!names || names.length === 0) && configSelected.size === 0) { toast('请选择配置表'); return; }
    const tables = (names && names.length) ? names : configTables.filter(t => configSelected.has(t.name)).map(t => t.name);
    setConfigStatus(`提取中（${tables.length} 张）…`, 'busy');
    log(`开始提取配置表：${tables.length} 张 → C# 类 + JSON 数据`);
    try {
        const res = await api('POST', '/api/config/extract', { Tables: tables, OutDir: outDir || null });
        if (!res.ok) { log('提取失败: ' + res.error, 'err'); setConfigStatus('失败', 'err'); return; }
        (res.messages || []).forEach(m => log(m, 'ok'));
        const ok = res.files.filter(f => f.classFile || f.dataFile).length;
        log(`完成：写出 ${ok}/${res.tableCount} 张配置表（每张含 .cs 类 与 .json 数据）`, 'ok');
        setConfigStatus(`已完成：${ok} 张`, 'ok');
        toast(`配置表提取完成：${ok} 张`);
    } catch (e) { log('提取异常: ' + e.message, 'err'); setConfigStatus('异常', 'err'); }
}

$('#btnLoadConfig').addEventListener('click', loadConfigTables);
$('#btnExtractConfigSel').addEventListener('click', () => extractConfig(null));
$('#btnExtractConfigAll').addEventListener('click', () => extractConfig(['*']));
$('#configSearch').addEventListener('input', renderConfigTable);
$('#btnConfigSelAll').addEventListener('click', () => { configTables.forEach(t => configSelected.add(t.name)); renderConfigTable(); });
$('#btnConfigClearAll').addEventListener('click', () => { configSelected.clear(); renderConfigTable(); });
$('#configChkAll').addEventListener('change', ev => {
    if (ev.target.checked) configTables.forEach(t => configSelected.add(t.name)); else configSelected.clear();
    renderConfigTable();
});
$('#configBaseUrl').addEventListener('input', updateConfigResolvedUrl);
$('#configVersion').addEventListener('input', updateConfigResolvedUrl);
$('#btnCopyConfigUrl').addEventListener('click', () => {
    const v = $('#configResolvedUrl').value;
    if (navigator.clipboard) navigator.clipboard.writeText(v).then(() => toast('已复制配置 URL')).catch(() => toast('复制失败'));
});

// ---------------- 初始化 ----------------
$('#btnClearLog').addEventListener('click', () => $('#log').innerHTML = '');
(async () => {
    switchView('overview');        // 启动默认进入「提取总览」
    updateConfigResolvedUrl();     // 实时拼出配置 URL
    try {
        const p = await api('GET', '/api/ping');
        log('服务已连接 ' + (p.time ? new Date(p.time).toLocaleTimeString() : ''), 'ok');
    } catch (e) {
        log('无法连接服务: ' + e.message, 'err');
    }
    try {
        const info = await api('GET', '/api/info');
        const dl = info.downloadDir || '';
        if (dl) {
            if (!$('#mapDest').value.trim()) $('#mapDest').value = dl;
            if (!$('#atlasOutDir').value.trim()) $('#atlasOutDir').value = dl;
            if (!$('#configOutDir').value.trim()) $('#configOutDir').value = dl + '\\Mir2Config';
        }
    } catch (e) { log('读取运行信息失败: ' + e.message, 'err'); }
})();
