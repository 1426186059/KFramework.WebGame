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
    if (v === 'browser') browserGo();
    if (v === 'settings') loadSettings();
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
    log('开始加载地图数据（按 Config.cs 中的 URL 自动下载）…');
    try {
        const res = await api('POST', '/api/map/load', {});
        if (!res.ok) { log('加载失败: ' + res.error, 'err'); return; }
        mapIndex = res.entries;
        mapSourceFile = res.sourceFile || '';
        mapLoaded = true;
        mapSelected.clear();
        $('#mapDatPath').value = mapSourceFile;
        setDot('mapDatPath', true);
        $('#datBadge').className = 'badge badge-green';
        $('#datBadge').textContent = `已加载 ${res.count} 张`;
        $('#mapCountInfo').textContent =
            `共 ${res.count} 张 · 配置 ${res.configCount} 条 · ${(res.totalBytes / 1048576).toFixed(2)} MB`;
        (res.messages || []).forEach(m => log(m, 'ok'));
        renderMapTable();
        log(`加载完成：${res.count} 张地图，平均 ${(res.totalBytes / res.count / 1024).toFixed(1)} KB/张`, 'ok');
    } catch (e) {
        log('加载异常: ' + e.message, 'err');
    } finally {
        btn.disabled = false;
        btn.textContent = '重新加载地图数据';
    }
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
        $('#previewStats').textContent =
            `网格 ${Math.sqrt(res.cells) | 0}? · 总格 ${res.cells} ｜ 可走 ${res.walkable} ｜ 遮挡 ${res.cover} ｜ 安全区 ${res.safe}`;
    } catch (e) { log('预览异常: ' + e.message, 'err'); }
}

$('#btnExportMap').addEventListener('click', async () => {
    const dest = $('#mapDest').value.trim();
    if (!mapSourceFile) { toast('请先加载地图数据'); return; }
    if (!dest) { toast('请设置导出目标目录'); return; }
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

// ---------------- 提取所有图集 ----------------
$('#btnAtlasScan').addEventListener('click', async () => {
    const root = $('#atlasRoot').value.trim();
    if (!root) { toast('请选择资源根目录'); return; }
    try {
        const res = await api('POST', '/api/atlas/scan', { path: root });
        if (!res.ok) { log('扫描失败: ' + res.error, 'err'); return; }
        renderAtlasList(res.files);
        $('#atlasInfo').textContent = `找到 ${res.files.length} 个图集文件`;
        log(`扫描到 ${res.files.length} 个图集`, 'ok');
    } catch (e) { log('扫描异常: ' + e.message, 'err'); }
});

function renderAtlasList(files) {
    const tb = $('#atlasTbody');
    tb.innerHTML = '';
    if (!files.length) { $('#atlasTableWrap').classList.add('hidden'); $('#atlasList').classList.remove('hidden'); return; }
    $('#atlasList').classList.add('hidden');
    $('#atlasTableWrap').classList.remove('hidden');
    files.forEach(f => {
        const tr = document.createElement('tr');
        tr.innerHTML = `<td>${f.name}</td><td>${f.kind}</td><td>${fmtSize(f.size)}</td>
            <td><button class="btn btn-ghost btn-xs act-info">解析</button></td>`;
        tr.querySelector('.act-info').addEventListener('click', () => atlasInfo(f.path, f.name));
        tb.appendChild(tr);
    });
}

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

// ---------------- 提取精灵素材 ----------------
$('#btnSpriteScan').addEventListener('click', async () => {
    const root = $('#spriteRoot').value.trim();
    if (!root) { toast('请选择资源根目录'); return; }
    const exts = ($('#spriteCat').value || 'wil,wzl,wzx').split(',').map(s => '.' + s.trim().replace(/^\./, ''));
    try {
        const res = await api('POST', '/api/scan', { root, exts });
        if (!res.ok) { log('扫描失败: ' + res.error, 'err'); return; }
        const tb = $('#spriteTbody');
        tb.innerHTML = '';
        if (!res.files.length) { $('#spriteTableWrap').classList.add('hidden'); $('#spriteList').classList.remove('hidden'); }
        else {
            $('#spriteList').classList.add('hidden'); $('#spriteTableWrap').classList.remove('hidden');
            res.files.forEach(f => {
                const tr = document.createElement('tr');
                tr.innerHTML = `<td>${f.name}</td><td>${f.kind}</td><td>${fmtSize(f.size)}</td>
                    <td><button class="btn btn-ghost btn-xs act-info">解析</button></td>`;
                tr.querySelector('.act-info').addEventListener('click', () => atlasInfo(f.path, f.name));
                tb.appendChild(tr);
            });
        }
        $('#spriteInfo').textContent = `找到 ${res.files.length} 个精灵库`;
        log(`扫描到 ${res.files.length} 个精灵库`, 'ok');
    } catch (e) { log('扫描异常: ' + e.message, 'err'); }
});

// ---------------- 提取音效音乐 ----------------
$('#btnAudioScan').addEventListener('click', async () => {
    const root = $('#audioRoot').value.trim();
    if (!root) { toast('请选择资源根目录'); return; }
    const exts = ['.wav', '.mp3', '.mid', '.midi', '.wma', '.ogg', '.lib'];
    try {
        const res = await api('POST', '/api/scan', { root, exts });
        if (!res.ok) { log('扫描失败: ' + res.error, 'err'); return; }
        const tb = $('#audioTbody');
        tb.innerHTML = '';
        if (!res.files.length) { $('#audioTableWrap').classList.add('hidden'); $('#audioList').classList.remove('hidden'); }
        else {
            $('#audioList').classList.add('hidden'); $('#audioTableWrap').classList.remove('hidden');
            res.files.forEach(f => {
                const tr = document.createElement('tr');
                tr.innerHTML = `<td>${f.name}</td><td>${f.kind}</td><td>${fmtSize(f.size)}</td>`;
                tb.appendChild(tr);
            });
        }
        $('#audioInfo').textContent = `找到 ${res.files.length} 个音频资源`;
        log(`扫描到 ${res.files.length} 个音频`, 'ok');
    } catch (e) { log('扫描异常: ' + e.message, 'err'); }
});

// ---------------- 解析 MirDB / 配置 ----------------
$('#btnMirDbInfo').addEventListener('click', async () => {
    const path = $('#mirdbPath').value.trim();
    if (!path) { toast('请选择 MirDB 文件'); return; }
    try {
        const res = await api('POST', '/api/file/info', { path });
        $('#mirdbResult').textContent = `${path}\n大小: ${fmtSize(res.size)}\n类型: ${res.kind}\n${res.note || ''}`;
        log(`MirDB 信息: ${fmtSize(res.size)}`, 'ok');
    } catch (e) { log('读取异常: ' + e.message, 'err'); }
});

$('#btnCfgInfo').addEventListener('click', async () => {
    const path = $('#cfgPath').value.trim();
    if (!path) { toast('请选择文件'); return; }
    try {
        const res = await api('POST', '/api/file/info', { path });
        $('#cfgResult').textContent = `${path}\n大小: ${fmtSize(res.size)}\n行数: ${res.lines}\n类型: ${res.kind}\n${res.note || ''}`;
        log(`配置信息: ${fmtSize(res.size)} / ${res.lines} 行`, 'ok');
    } catch (e) { log('读取异常: ' + e.message, 'err'); }
});

// ---------------- 资源浏览器 ----------------
async function browserGo() {
    const p = $('#browserPath').value.trim();
    let list;
    try {
        list = await api('GET', `/api/fs?path=${encodeURIComponent(p)}&files=${$('#browserFiles').checked}&filter=${encodeURIComponent($('#browserFilter').value.trim())}`);
    } catch (e) { log('浏览失败: ' + e.message, 'err'); return; }
    const box = $('#browserList');
    box.innerHTML = '';
    list.forEach(it => {
        const div = document.createElement('div');
        div.className = 'browse-item';
        div.innerHTML = `<span class="ic">${it.isDir ? '📁' : '📄'}</span><span>${it.name}${it.isDir ? '' : '  ·  ' + fmtSize(it.size)}</span>`;
        div.addEventListener('click', () => {
            if (it.isDir) { $('#browserPath').value = it.path; browserGo(); }
            else { log('文件: ' + it.path + '  (' + fmtSize(it.size) + ')'); }
        });
        box.appendChild(div);
    });
}
$('#btnBrowserGo').addEventListener('click', browserGo);
$('#btnBrowserUp').addEventListener('click', () => {
    const p = $('#browserPath').value.trim();
    const up = p.replace(/[\\/][^\\/]+$/, '');
    if (up && up !== p) { $('#browserPath').value = up; browserGo(); }
});
$('#browserFiles').addEventListener('change', browserGo);
$('#browserFilter').addEventListener('input', browserGo);

// ---------------- 运行配置 ----------------
async function loadSettings() {
    try {
        const res = await api('GET', '/api/info');
        $('#settingsInfo').textContent =
            `运行状态: 正常\n操作系统: ${res.os}\n框架: ${res.framework}\n工作目录: ${res.cwd}\n静态根: wwwroot`;
    } catch (e) {
        $('#settingsInfo').textContent = '获取运行信息失败: ' + e.message;
    }
}

// ---------------- 初始化 ----------------
$('#btnClearLog').addEventListener('click', () => $('#log').innerHTML = '');
(async () => {
    try {
        const p = await api('GET', '/api/ping');
        $('#pingBadge').className = 'badge badge-green';
        $('#pingBadge').textContent = '已连接';
        log('服务已连接 ' + (p.time ? new Date(p.time).toLocaleTimeString() : ''), 'ok');
    } catch (e) {
        $('#pingBadge').className = 'badge badge-red';
        $('#pingBadge').textContent = '未连接';
        log('无法连接服务: ' + e.message, 'err');
    }
})();
