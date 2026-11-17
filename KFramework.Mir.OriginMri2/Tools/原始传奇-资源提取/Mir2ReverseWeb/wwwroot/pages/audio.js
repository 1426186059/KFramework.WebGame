'use strict';

function setAudioStatus(text, cls) {
    const el = $('#audioStatus');
    if (el) { el.textContent = text; el.className = 'dl-status ' + (cls || ''); }
}

// 收集音频 id（从 0~5 配置分片递归扫描 sound/file 字段）
async function collectAudioIds() {
    const btn = $('#btnCollectIds');
    btn.disabled = true; btn.textContent = '收集中…';
    setAudioStatus('正在下载并扫描配置分片…');
    try {
        const res = await api('POST', '/api/audio/ids', {
            baseUrl: $('#configBaseUrl').value.trim() || null,
            version: $('#configVersion').value.trim() || null
        });
        (res.messages || []).forEach(m => log(m, 'ok'));
        if (!res.ok && !res.total) { log('收集失败: ' + (res.error || '未知错误'), 'err'); setAudioStatus('失败', 'err'); return; }
        $('#audioIds').value = (res.ids || []).join(',');
        $('#audioIdInfo').textContent = `共 ${res.total} 个音频 id`;
        setAudioStatus(`已收集 ${res.total} 个 id`, 'ok');
        log(`收集完成：${res.total} 个音频 id`, 'ok');
    } catch (e) {
        log('收集异常: ' + e.message, 'err');
        setAudioStatus('异常', 'err');
    } finally {
        btn.disabled = false; btn.textContent = '收集音频 id';
    }
}

// 提取所有音频到输出目录
async function extractAudio() {
    const btn = $('#btnExtractAudio');
    btn.disabled = true; btn.textContent = '提取中…';
    setAudioStatus('提取中…');
    const raw = $('#audioIds').value.trim();
    let ids = [];
    if (raw) {
        ids = raw.split(/[,\s]+/).filter(Boolean).map(x => parseInt(x, 10)).filter(x => !isNaN(x) && x > 0);
    }
    try {
        const res = await api('POST', '/api/audio/extract', {
            ids: ids,
            outDir: $('#audioOutDir').value.trim() || null,
            fromConfig: ids.length === 0
        });
        (res.messages || []).forEach(m => log(m, m.indexOf('[进度]') === 0 ? '' : 'ok'));
        if (!res.ok && !res.downloaded && !res.skipped) { log('提取失败: ' + (res.error || '未知错误'), 'err'); setAudioStatus('失败', 'err'); return; }
        setAudioStatus(`完成：下载 ${res.downloaded} 跳过 ${res.skipped} 失败 ${res.failed}`, 'ok');
        log(`提取完成：共 ${res.total}，下载 ${res.downloaded}，跳过 ${res.skipped}，失败 ${res.failed}`, 'ok');
        toast('音频提取完成');
    } catch (e) {
        log('提取异常: ' + e.message, 'err');
        setAudioStatus('异常', 'err');
    } finally {
        btn.disabled = false; btn.textContent = '提取所有音频';
    }
}

$('#btnCollectIds').addEventListener('click', collectAudioIds);
$('#btnExtractAudio').addEventListener('click', extractAudio);
