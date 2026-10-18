'use strict';

// 按配置表提取资源（统一模式）：分类下拉 → 列出该配置表条目 → 选中 → 导出
// 复用后端 /api/anim/categories、/api/anim/category-entries、/api/anim/export、/api/anim/progress
// 注意：ASP.NET Core 默认把响应属性序列化为小驼峰（ok/error/models…），字段名须对应。

const res = {
    all: [],            // [{key, name, models:[int]}]
    selected: new Set(),
    jobId: null,
    timer: null,
};

// ---------------- 进度轮询（独立，不复用 anim.js） ----------------
function uniSetProgress(pct, text) {
    const w = $('#uniProgressWrap'), bar = $('#uniProgressBar');
    if (w) w.classList.remove('hidden');
    if (bar) { bar.classList.remove('indeterminate'); bar.style.width = Math.max(0, Math.min(100, pct)) + '%'; }
    const el = $('#resStatus');
    if (el) { el.textContent = text; el.className = 'dl-status'; }
}

function uniTimerStart() {
    if (res.timer) clearInterval(res.timer);
    res.timer = setInterval(uniPoll, 1000);
    uniPoll();
}

async function uniPoll() {
    if (!res.jobId) return;
    let p;
    try { p = await api('GET', '/api/anim/progress?job=' + encodeURIComponent(res.jobId)); }
    catch (e) { return; }
    const done = p.doneFiles, total = p.totalFiles;
    const pct = total > 0 ? Math.round(100 * done / total) : (p.status === 'done' ? 100 : 0);
    uniSetProgress(pct, `[${pct}%] ${done}/${total}　失败 ${p.failed}　跳过 ${p.skipped}　${(p.bytes / 1048576).toFixed(1)} MB${p.current ? '　← ' + p.current : ''}`);
    if (p.status !== 'running') {
        if (res.timer) { clearInterval(res.timer); res.timer = null; }
        (p.messages || []).forEach(m => log(m));
        log(p.status === 'done' ? '导出完成 ✅' : ('导出异常：' + (p.messages || []).join(' ')), p.status === 'done' ? 'ok' : 'err');
        const btn = $('#btnResExport'); if (btn) btn.disabled = false;
    }
}

// ---------------- 分类与条目 ----------------
async function resInitCats() {
    try {
        const r = await api('GET', '/api/anim/categories');
        const sel = $('#resCat');
        sel.innerHTML = '';
        (r || []).forEach(c => { const o = document.createElement('option'); o.value = c.id; o.textContent = c.label; sel.appendChild(o); });
    } catch (e) { log('加载分类失败: ' + e.message, 'err'); }
}

async function resLoad() {
    const cat = $('#resCat').value;
    if (!cat) return;
    const btn = $('#btnResLoad'); btn.disabled = true;
    log('加载分类「' + cat + '」的资源配置条目…');
    try {
        const q = $('#resSearch').value.trim();
        const r = await api('GET', '/api/anim/category-entries?cat=' + encodeURIComponent(cat) + '&q=' + encodeURIComponent(q));
        if (!r.ok) { log('加载失败: ' + (r.error || ''), 'err'); $('#resLoadInfo').textContent = ''; return; }
        res.all = r.rows || [];
        res.selected.clear();
        resRender();
        $('#resListWrap').classList.remove('hidden');
        $('#btnResExport').disabled = res.all.length === 0;
        $('#resLoadInfo').textContent = `共 ${r.count} 条`;
        log(`分类「${cat}」：${r.count} 条资源条目`, 'ok');
    } catch (e) { log('加载失败: ' + e.message, 'err'); }
    finally { btn.disabled = false; }
}

function resRender() {
    const tb = $('#resTbody'); tb.innerHTML = '';
    res.all.forEach((e, i) => {
        const tr = document.createElement('tr'); tr.dataset.id = e.key;
        tr.innerHTML = `
            <td><input type="checkbox" class="rc" data-key="${e.key}" ${res.selected.has(e.key) ? 'checked' : ''}/></td>
            <td>${i + 1}</td>
            <td>${e.key}</td>
            <td class="title-cn">${e.name || '—'}</td>
            <td>${(e.models || []).join(',')}</td>
            <td><button class="btn btn-ghost btn-xs act-exp">导出</button></td>`;
        tr.querySelector('.rc').addEventListener('change', ev => {
            if (ev.target.checked) res.selected.add(e.key); else res.selected.delete(e.key);
            resUpdateSel();
        });
        tr.querySelector('.act-exp').addEventListener('click', ev => { ev.stopPropagation(); resExport([e.key]); });
        tb.appendChild(tr);
    });
    resFilterTable();
    resUpdateSel();
}

// 在已加载结果里实时过滤（不重新请求服务端），按 名字/模型号
function resFilterTable() {
    const q = ($('#resFilter').value || '').trim().toLowerCase();
    const rows = $('#resTbody').querySelectorAll('tr');
    let shown = 0;
    rows.forEach(tr => {
        const key = (tr.dataset.id || '').toLowerCase();
        const name = (tr.querySelector('.title-cn') ? tr.querySelector('.title-cn').textContent : '').toLowerCase();
        const hit = !q || key.includes(q) || name.includes(q);
        tr.style.display = hit ? '' : 'none';
        if (hit) shown++;
    });
    const info = $('#resSelInfo');
    if (info) info.textContent = `显示 ${shown} / ${res.all.length}` + (res.selected.size ? `　已选 ${res.selected.size}` : '');
}

function resUpdateSel() {
    const n = res.selected.size;
    const btn = $('#btnResExport');
    if (btn) btn.textContent = n === 0 ? '导出选中 资源' : `导出选中 (${n}) 资源`;
    const info = $('#resSelInfo');
    if (info) info.textContent = `已选 ${n} / ${res.all.length}`;
}

async function resExport(onlyKeys) {
    let keys;
    if (Array.isArray(onlyKeys)) keys = onlyKeys;
    else if (res.selected.size > 0) keys = Array.from(res.selected);
    else keys = [];
    if (keys.length === 0) { toast('请先选择条目'); return; }
    const set = new Set();
    for (const e of res.all) { if (keys.includes(e.key)) (e.models || []).forEach(m => set.add(String(m))); }
    const models = Array.from(set);
    const btn = $('#btnResExport'); btn.disabled = true;
    const body = { Models: models.join(','), OutDir: $('#uniOutDir').value.trim(), WithJson: $('#uniWithJson').checked, MaxAction: parseInt($('#uniMaxAction').value, 10) || 60 };
    log(`提交统一资源导出任务（${models.length} 个模型）…`);
    try {
        const r = await api('POST', '/api/anim/export', body);
        if (!r.ok) { log('导出失败: ' + (r.error || ''), 'err'); btn.disabled = false; return; }
        res.jobId = r.jobId;
        log(`任务已启动：共 ${r.totalFiles} 个动作组（job=${r.jobId}）`);
        uniTimerStart();
    } catch (e) { log('导出失败: ' + e.message, 'err'); btn.disabled = false; }
}

const resCatEl = $('#resCat'); if (resCatEl) resCatEl.addEventListener('change', resLoad);
const resLoadBtn = $('#btnResLoad'); if (resLoadBtn) resLoadBtn.addEventListener('click', resLoad);
const resSelAllBtn = $('#btnResSelAll'); if (resSelAllBtn) resSelAllBtn.addEventListener('click', () => { res.all.forEach(e => res.selected.add(e.key)); resRender(); });
const resClearAllBtn = $('#btnResClearAll'); if (resClearAllBtn) resClearAllBtn.addEventListener('click', () => { res.selected.clear(); resRender(); });
const resChkAll = $('#resChkAll'); if (resChkAll) resChkAll.addEventListener('change', ev => { if (ev.target.checked) res.all.forEach(e => res.selected.add(e.key)); else res.selected.clear(); resRender(); });
const resExportBtn = $('#btnResExport'); if (resExportBtn) resExportBtn.addEventListener('click', () => resExport());
const resFilterEl = $('#resFilter'); if (resFilterEl) resFilterEl.addEventListener('input', resFilterTable);
resInitCats();
