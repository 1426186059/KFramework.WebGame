'use strict';

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

// ---------------- 图集页初始化 ----------------
$('#btnLoadManifest').addEventListener('click', loadManifest);
$('#btnDownloadAll').addEventListener('click', downloadAll);
$('#manifestSearch').addEventListener('input', renderManifestTable);
