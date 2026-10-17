'use strict';

// 角色 / NPC / 怪物 动画精灵表导出页
// 注意：ASP.NET Core 默认把响应属性序列化为小驼峰（ok/error/modelCount…），此处字段名须与之对应。

const anim = {
    loaded: false,
    jobId: null,
    timer: null,
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
        $('#animModelCount').textContent = r.modelCount;
        const models = r.models || [];
        const hint = models.length > 300
            ? models.slice(0, 300).join(', ') + ` …（共 ${models.length} 个，可在上方筛选框指定部分）`
            : models.join(', ');
        $('#animModelHint').value = hint || '（无）';
        $('#btnExportAnim').disabled = false;
        log(`解析完成：${r.modelCount} 个模型号，可点「导出」批量下载`, 'ok');
    } catch (e) {
        log('加载失败: ' + e.message, 'err');
    } finally {
        btn.disabled = false;
    }
}

async function animExport() {
    if (!anim.loaded) { toast('请先加载并解析模型清单'); return; }
    const btn = $('#btnExportAnim');
    btn.disabled = true;
    const body = {
        Models: $('#animModels').value.trim(),
        OutDir: $('#animOutDir').value.trim(),
        WithJson: $('#animWithJson').checked,
        MaxAction: parseInt($('#animMaxAction').value, 10) || 60,
    };
    log('提交导出任务…');
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
const animBtnExport = $('#btnExportAnim');
if (animBtnExport) animBtnExport.addEventListener('click', animExport);
