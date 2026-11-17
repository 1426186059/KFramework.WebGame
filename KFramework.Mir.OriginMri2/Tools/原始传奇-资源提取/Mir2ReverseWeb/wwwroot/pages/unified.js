'use strict';

// 按配置表提取资源（统一模式）：扫描所有配置表，一次导出 model 帧 + 物品图标 + 技能图标。
// 后端：/api/unified/extract、/api/unified/progress。ASP.NET Core 默认小驼峰序列化响应字段。

const uni = {
    jobId: null,
    timer: null,
};

// 默认输出目录已直接写死在 unified.html 的 input value 中（Mir2Res 下真实相对路径），无需 JS 异步填充。

// ---------------- 进度轮询 ----------------
function uniSetProgress(pct, text) {
    const w = $('#uniProgressWrap'), bar = $('#uniProgressBar');
    if (w) w.classList.remove('hidden');
    if (bar) { bar.classList.remove('indeterminate'); bar.style.width = Math.max(0, Math.min(100, pct)) + '%'; }
    const el = $('#resStatus');
    if (el) { el.textContent = text; el.className = 'dl-status'; }
}

function uniTimerStart() {
    if (uni.timer) clearInterval(uni.timer);
    uni.timer = setInterval(uniPoll, 1000);
    uniPoll();
}

async function uniPoll() {
    if (!uni.jobId) return;
    let p;
    try { p = await api('GET', '/api/unified/progress?job=' + encodeURIComponent(uni.jobId)); }
    catch (e) { return; }
    if (p.ok === false) return;
    const done = p.doneFiles, total = p.totalFiles;
    const pct = p.percent !== undefined ? p.percent : (total > 0 ? Math.round(100 * done / total) : (p.status === 'done' ? 100 : 0));
    uniSetProgress(pct, `[${pct}%] ${done}/${total}　成功 ${p.success || 0}　跳过 ${p.skipped || 0}　失败 ${p.failed || 0}　${((p.bytes || 0) / 1048576).toFixed(1)} MB${p.current ? '　← ' + p.current : ''}`);
    if (p.status !== 'running') {
        if (uni.timer) { clearInterval(uni.timer); uni.timer = null; }
        (p.messages || []).forEach(m => log(m));
        log(p.status === 'done' ? '统一提取完成 ✅' : ('提取异常：' + (p.messages || []).join(' ')), p.status === 'done' ? 'ok' : 'err');
        const btn = $('#btnUniExtract'); if (btn) btn.disabled = false;
    }
}

// ---------------- 统一提取 ----------------
async function uniExtract() {
    const btn = $('#btnUniExtract');
    btn.disabled = true;
    const body = {
        ModelOutDir: $('#uniModelDir').value.trim(),
        ItemIconOutDir: $('#uniItemIconDir').value.trim(),
        SkillIconOutDir: $('#uniSkillIconDir').value.trim(),
        WithJson: $('#uniWithJson').checked,
        MaxAction: parseInt($('#uniMaxAction').value, 10) || 60
    };
    log('提交统一提取任务（全配置表扫描：model 帧 + 物品图标 + 技能图标）…');
    try {
        const r = await api('POST', '/api/unified/extract', body);
        if (!r.ok) { log('提交失败: ' + (r.error || ''), 'err'); btn.disabled = false; return; }
        uni.jobId = r.jobId;
        log(`任务已启动：共 ${r.totalFiles} 个任务项（job=${r.jobId}）`);
        uniTimerStart();
    } catch (e) { log('提交失败: ' + e.message, 'err'); btn.disabled = false; }
}

const uniBtn = $('#btnUniExtract'); if (uniBtn) uniBtn.addEventListener('click', uniExtract);
