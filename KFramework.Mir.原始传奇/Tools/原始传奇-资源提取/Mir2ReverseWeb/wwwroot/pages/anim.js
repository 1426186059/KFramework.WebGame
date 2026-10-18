'use strict';

// 角色 / NPC / 怪物 动画精灵表导出页
// 注意：ASP.NET Core 默认把响应属性序列化为小驼峰（ok/error/models…），此处字段名须与之对应。

const anim = {
    loaded: false,
    jobId: null,
    timer: null,
    allModels: [],          // [{id, name, version}]
    selected: new Set(),    // 选中的模型号字符串
};

function animSetProgress(pct, text) {
    const w = $('#animProgressWrap'), bar = $('#animProgressBar');
    if (w) w.classList.remove('hidden');
    if (bar) {
        bar.classList.remove('indeterminate');
        bar.style.width = Math.max(0, Math.min(100, pct)) + '%';
    }
    const el = $('#animStatus');
    if (el) { el.textContent = text; el.className = 'dl-status'; }
}

function animUpdateSel() {
    const n = anim.selected.size;
    const btn = $('#btnExportAnim');
    if (btn) {
        if (n === 0) btn.textContent = '导出全部 角色/NPC/怪物 动画精灵表';
        else btn.textContent = `导出选中 (${n}) 角色/NPC/怪物 动画精灵表`;
    }
    const info = $('#animSelInfo');
    if (info) info.textContent = `已选 ${n} / ${anim.allModels.length}`;
}

function animRenderTable() {
    const tb = $('#animTbody');
    tb.innerHTML = '';
    anim.allModels.forEach((e, i) => {
        const tr = document.createElement('tr');
        tr.dataset.id = e.id;
        tr.innerHTML = `
            <td><input type="checkbox" class="ac" data-id="${e.id}" ${anim.selected.has(e.id) ? 'checked' : ''}/></td>
            <td>${i + 1}</td>
            <td>${e.id}</td>
            <td class="title-cn">${e.name || '—'}</td>
            <td>${e.version || '—'}</td>
            <td><button class="btn btn-ghost btn-xs act-exp">导出</button></td>`;
        tr.querySelector('.ac').addEventListener('change', ev => {
            if (ev.target.checked) anim.selected.add(e.id); else anim.selected.delete(e.id);
            animUpdateSel();
        });
        tr.querySelector('.act-exp').addEventListener('click', ev => { ev.stopPropagation(); animExport([e.id]); });
        tb.appendChild(tr);
    });
    $('#animListWrap').classList.remove('hidden');
    animUpdateSel();
}

function animFilterTable() {
    const q = $('#animSearch').value.trim().toLowerCase();
    const rows = $('#animTbody').querySelectorAll('tr');
    let shown = 0;
    rows.forEach(tr => {
        const id = (tr.dataset.id || '').toLowerCase();
        const name = (tr.querySelector('.title-cn') ? tr.querySelector('.title-cn').textContent : '').toLowerCase();
        const hit = !q || id.includes(q) || name.includes(q);
        tr.style.display = hit ? '' : 'none';
        if (hit) shown++;
    });
    const info = $('#animSelInfo');
    if (info) info.textContent = `显示 ${shown} / ${anim.allModels.length}` + (anim.selected.size ? `　已选 ${anim.selected.size}` : '');
}

async function animLoad() {
    const btn = $('#btnLoadVersion');
    btn.disabled = true;
    log('加载 resourceVersion.json（解析模型清单）…');
    try {
        const r = await api('POST', '/api/anim/load');
        (r.messages || []).forEach(m => log(m));
        if (!r.ok) { log('加载失败: ' + (r.error || '（服务未返回具体原因，详见上方日志）'), 'err'); return; }
        anim.loaded = true;
        $('#animLoadInfo').textContent = `共 ${r.totalEntries} 个 model 资源，涉及 ${r.modelCount} 个模型号`;
        anim.allModels = r.models || [];
        anim.selected.clear();
        animRenderTable();
        animFilterTable();
        $('#btnExportAnim').disabled = false;
        log(`解析完成：${r.modelCount} 个模型号，可在上方列表中勾选导出`, 'ok');
    } catch (e) {
        log('加载失败: ' + e.message, 'err');
    } finally {
        btn.disabled = false;
    }
}

async function animExport(onlyIds) {
    if (!anim.loaded && !(Array.isArray(onlyIds) && onlyIds.length)) { toast('请先加载并解析模型清单'); return; }
    let ids;
    if (Array.isArray(onlyIds)) ids = onlyIds;
    else if (anim.selected.size > 0) ids = Array.from(anim.selected);
    else ids = [];   // 空 = 全部
    const btn = $('#btnExportAnim');
    btn.disabled = true;
    const body = {
        Models: ids.join(','),
        OutDir: $('#animOutDir').value.trim(),
        WithJson: $('#animWithJson').checked,
        MaxAction: parseInt($('#animMaxAction').value, 10) || 60,
    };
    log(ids.length ? `提交导出任务（${ids.length} 个模型）…` : '提交导出任务（全部模型）…');
    try {
        const r = await api('POST', '/api/anim/export', body);
        if (!r.ok) { log('导出失败: ' + (r.error || '（服务未返回具体原因，详见上方日志）'), 'err'); btn.disabled = false; return; }
        anim.jobId = r.jobId;
        log(`任务已启动：共 ${r.totalFiles} 个动作组（job=${r.jobId}）`);
        animTimerStart();
    } catch (e) {
        log('导出失败: ' + e.message, 'err');
        btn.disabled = false;
    }
}

function animTimerStart() {
    if (anim.timer) clearInterval(anim.timer);
    anim.timer = setInterval(animPoll, 1000);
    animPoll();
}

async function animPoll() {
    if (!anim.jobId) return;
    let p;
    try { p = await api('GET', '/api/anim/progress?job=' + encodeURIComponent(anim.jobId)); }
    catch (e) { return; }
    const done = p.doneFiles, total = p.totalFiles;
    const pct = total > 0 ? Math.round(100 * done / total) : (p.status === 'done' ? 100 : 0);
    animSetProgress(pct, `[${pct}%] ${done}/${total}　失败 ${p.failed}　跳过 ${p.skipped}　${(p.bytes / 1048576).toFixed(1)} MB${p.current ? '　← ' + p.current : ''}`);
    if (p.status !== 'running') {
        if (anim.timer) { clearInterval(anim.timer); anim.timer = null; }
        (p.messages || []).forEach(m => log(m));
        log(p.status === 'done' ? '导出完成 ✅' : ('导出异常：' + (p.messages || []).join(' ')), p.status === 'done' ? 'ok' : 'err');
        const btn = $('#btnExportAnim'); if (btn) btn.disabled = false;
    }
}

const animBtnLoad = $('#btnLoadVersion');
if (animBtnLoad) animBtnLoad.addEventListener('click', animLoad);

const animBtnRefresh = $('#btnRefreshNames');
if (animBtnRefresh) animBtnRefresh.addEventListener('click', async () => {
    const btn = animBtnRefresh; btn.disabled = true;
    log('从 CDN 原始配置(5config) 重新生成 modelNames.json 特效命名…');
    try {
        const r = await api('POST', '/api/anim/refresh-names');
        if (!r.ok) { log('刷新失败: ' + (r.error || ''), 'err'); }
        else {
            log(`刷新完成：更新 ${r.updated} 条特效命名（共 ${r.total} 条），重新加载清单…`, 'ok');
            await animLoad();
        }
    } catch (e) { log('刷新失败: ' + e.message, 'err'); }
    finally { btn.disabled = false; }
});
const animBtnExport = $('#btnExportAnim');
if (animBtnExport) animBtnExport.addEventListener('click', () => animExport());

const animSearchEl = $('#animSearch');
if (animSearchEl) animSearchEl.addEventListener('input', animFilterTable);
const animSelAllBtn = $('#btnAnimSelAll');
if (animSelAllBtn) animSelAllBtn.addEventListener('click', () => { anim.allModels.forEach(e => anim.selected.add(e.id)); animRenderTable(); animFilterTable(); });
const animClearAllBtn = $('#btnAnimClearAll');
if (animClearAllBtn) animClearAllBtn.addEventListener('click', () => { anim.selected.clear(); animRenderTable(); animFilterTable(); });
const animChkAll = $('#animChkAll');
if (animChkAll) animChkAll.addEventListener('change', ev => {
    if (ev.target.checked) anim.allModels.forEach(e => anim.selected.add(e.id)); else anim.selected.clear();
    animRenderTable(); animFilterTable();
});
