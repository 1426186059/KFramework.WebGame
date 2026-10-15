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

// ---------------- 视图切换 ----------------
function switchView(v) {
    $$('.nav-item').forEach(n => n.classList.toggle('active', n.dataset.view === v));
    $$('.view').forEach(s => s.classList.toggle('hidden', s.id !== 'view-' + v));
}

$$('.nav-item').forEach(n => n.addEventListener('click', () => {
    const v = n.dataset.view;
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
        tr.querySelector('.act-prev').addEventListener('click', ev => { ev.stopPropagation(); previewMap(e.id); });
        tr.addEventListener('click', () => previewMap(e.id));
        tb.appendChild(tr);
        n++;
    });
    $('#mapListWrap').classList.remove('hidden');
    $('#mapSelInfo').textContent = `显示 ${n} 张`;
    updateMapSel();
}

function updateMapSel() {
    const n = mapSelected.size;
    const btn = $('#btnExportMap');
    btn.disabled = n === 0;
    btn.textContent = `提取选中的 ${n} 张地图`;
}

$('#mapSearch').addEventListener('input', renderMapTable);
$('#btnMapSelAll').addEventListener('click', () => { mapIndex.forEach(e => mapSelected.add(e.id)); renderMapTable(); });
$('#btnMapClearAll').addEventListener('click', () => { mapSelected.clear(); renderMapTable(); });
$('#mapChkAll').addEventListener('change', ev => {
    if (ev.target.checked) mapIndex.forEach(e => mapSelected.add(e.id)); else mapSelected.clear();
    renderMapTable();
});

function findEntry(id) { return mapIndex.find(e => e.id === id); }

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
        let stats = `网格 ${res.cols} × ${res.rows} ｜ 总格 ${res.cells}（数据块 ${res.rawCells}）｜ 可走 ${res.walkable} ｜ 遮挡 ${res.cover} ｜ 安全区 ${res.safe}`;
        if (res.warn) stats += `\n⚠ ${res.warn}`;
        $('#previewStats').textContent = stats;
    } catch (e) { log('预览异常: ' + e.message, 'err'); }
}

$('#btnExportMap').addEventListener('click', async () => {
    const dest = $('#mapDest').value.trim();
    if (!mapSourceFile) { toast('请先加载地图数据'); return; }
    if (!dest) { toast('请设置输出目录'); return; }
    const ids = [...mapSelected];
    try {
        log(`导出 ${ids.length} 张地图到 ${dest} ...`);
        const res = await api('POST', '/api/map/export', {
            path: '', ids, destination: dest
        });
        if (!res.ok) { log('导出失败: ' + res.error, 'err'); return; }
        log(`已导出 ${res.exported} 张 PNG`, 'ok');
        toast(`导出完成：${res.exported} 张`);
    } catch (e) { log('导出异常: ' + e.message, 'err'); }
});

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
    try {
        const res = await api('POST', '/api/atlas/download', { key, outDir: $('#atlasOutDir').value.trim() });
        if (!res.ok) { log(`下载 ${key} 失败: ${res.error}`, 'err'); return; }
        if (res.skipped) log(`已存在(跳过) ${key} -> ${res.localPath}`, 'ok');
        else log(`已下载 ${key} (${(res.bytes / 1024).toFixed(1)} KB) -> ${res.localPath}`, 'ok');
    } catch (e) { log(`下载异常 ${key}: ${e.message}`, 'err'); }
}

async function downloadAll() {
    const btn = $('#btnDownloadAll');
    btn.disabled = true; btn.textContent = '下载中…';
    try {
        const res = await api('POST', '/api/atlas/downloadAll', { outDir: $('#atlasOutDir').value.trim() });
        if (!res.ok) { log('全部下载失败: ' + res.error, 'err'); return; }
        (res.messages || []).forEach(m => log(m, 'ok'));
    } catch (e) { log('全部下载异常: ' + e.message, 'err'); }
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



// ---------------- 初始化 ----------------
$('#btnClearLog').addEventListener('click', () => $('#log').innerHTML = '');
(async () => {
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
        }
    } catch (e) { log('读取运行信息失败: ' + e.message, 'err'); }
})();
