'use strict';

const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];

// ---------------- 基础工具（各页共用） ----------------
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
    if (!box) return;
    const line = document.createElement('div');
    if (cls) line.className = cls;
    const t = new Date().toLocaleTimeString();
    line.textContent = `[${t}] ${msg}`;
    box.appendChild(line);
    box.scrollTop = box.scrollHeight;
}

function toast(msg) {
    const el = $('#toast');
    if (!el) return;
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

// 列表渲染（地图 / 图集共用）
function renderUrlList(el, urls) {
    if (!el) return;
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

// ---------------- 浏览弹窗（各页共用） ----------------
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

// ---------------- 共享外壳事件（所有页面都绑定，元素缺失时自动跳过） ----------------
const backBtn = $('#btnBack');
if (backBtn) backBtn.addEventListener('click', () => location.href = 'index.html');
$('#browseClose').addEventListener('click', () => $('#browseModal').classList.add('hidden'));
$('#browseUp').addEventListener('click', () => {
    const p = browse.path;
    const up = p.replace(/[\\/][^\\/]+$/, '');
    if (up && up !== browse.path) { browse.path = up; browseRender(); }
});
$('#browsePick').addEventListener('click', () => pickPath(browse.path));
$$('[data-browse]').forEach(b => b.addEventListener('click', () => openBrowse(b.dataset.browse, 'dir', '')));
$$('[data-browse-file]').forEach(b => b.addEventListener('click', () => openBrowse(b.dataset.browseFile, 'file', b.dataset.filter || '')));
$('#btnClearLog').addEventListener('click', () => { const l = $('#log'); if (l) l.innerHTML = ''; });

// ---------------- 初始化（各页共用：探活 + 默认值） ----------------
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
            const setIfEmpty = (id, val) => { const el = $('#' + id); if (el && !el.value.trim()) el.value = val; };
            setIfEmpty('mapDest', dl);
            setIfEmpty('atlasOutDir', dl);
            setIfEmpty('configOutDir', dl + '\\Mir2Config');
            setIfEmpty('animOutDir', dl + '\\Mir2Anim');
        }
    } catch (e) { log('读取运行信息失败: ' + e.message, 'err'); }
})();
