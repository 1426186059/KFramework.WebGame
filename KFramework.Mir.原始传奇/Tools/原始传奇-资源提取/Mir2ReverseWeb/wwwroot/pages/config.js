'use strict';

let configTables = [];                 // [{name, kind, count, fields:[{name,csType,optional}]}]
const configSelected = new Set();      // selected table names

function setConfigStatus(text, cls) {
    const el = $('#configStatus');
    if (el) { el.textContent = text; el.className = 'dl-status ' + (cls || ''); }
}

// 根据 数据服务器地址 + 版本号 实时拼出全部配置分片 URL（与游戏 onLoadCfgComplete2 循环一致：0config~5config）
function updateConfigResolvedUrl() {
    const base = ($('#configBaseUrl').value.trim() || 'https://cdn.yscq-wy.yscq.com/fugu_20201203').replace(/\/$/, '');
    const ver = $('#configVersion').value.trim() || '10.25341.1';
    const urls = [];
    for (let i = 0; i < 6; i++) urls.push(`${base}/${i}config${ver}.json?v=${ver}`);
    $('#configResolvedUrl').value = urls.join('\n');
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

// ---------------- 配置页初始化 ----------------
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

// 预填拼接 URL
updateConfigResolvedUrl();
