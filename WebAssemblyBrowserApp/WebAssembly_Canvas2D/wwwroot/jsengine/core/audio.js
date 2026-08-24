// =====================================================================
// 音效层：WebAudio
//   - beep(freq, dur, waveType, vol)   合成音（无需外部资源）
//   - load(url)                        预加载 mp3/wav/ogg（可省略，播放时自动加载）
//   - play(url, loop, volume)          播放音频文件
//   - stop(url)                        停止该 url 的播放
//   - setVolume(url, volume)           实时调整音量
// 音频上下文须由用户手势解锁（ensure()），在输入事件中调用。
// =====================================================================

let _ctx = null
const _buffers = {}   // url -> AudioBuffer | Promise | null
const _playing = {}   // url -> { source, gain }

function ctx() {
    if (!_ctx) {
        const AC = window.AudioContext || window.webkitAudioContext
        if (AC) _ctx = new AC()
    }
    if (_ctx && _ctx.state === 'suspended') _ctx.resume()
    return _ctx
}

// 异步解码并缓存；同一 url 只解码一次。失败返回 null，不抛异常。
function loadBuffer(url) {
    if (url in _buffers) return _buffers[url]
    const p = fetch(url)
        .then(r => { if (!r.ok) throw new Error('HTTP ' + r.status); return r.arrayBuffer() })
        .then(buf => { const ac = ctx(); if (!ac) return null; return ac.decodeAudioData(buf) })
        .then(b => { _buffers[url] = b || null; return _buffers[url] })
        .catch(e => { console.warn('[audio] load failed:', url, e); _buffers[url] = null; return null })
    _buffers[url] = p
    return p
}

export const audio = {
    init() { _ctx = null },

    ensure() { ctx() },

    beep(freq, dur, waveType, vol) {
        const ac = ctx()
        if (!ac) return
        try {
            const osc = ac.createOscillator()
            const gain = ac.createGain()
            osc.type = waveType || 'square'
            osc.frequency.value = freq
            const t = ac.currentTime
            const v = vol || 0.08
            gain.gain.setValueAtTime(v, t)
            gain.gain.exponentialRampToValueAtTime(0.0001, t + (dur || 0.1))
            osc.connect(gain)
            gain.connect(ac.destination)
            osc.start(t)
            osc.stop(t + (dur || 0.1) + 0.02)
        } catch { /* 忽略音频异常 */ }
    },

    // 预加载音频文件。播放时也会自动加载，可不预调。
    load(url) { loadBuffer(url) },

    // 播放音频文件（mp3/wav/ogg）。loop 是否循环，volume 0~1。
    // 同 url 重复调用：若正在播则先停止再重播。
    play(url, loop, volume) {
        const ac = ctx()
        if (!ac) return
        loadBuffer(url).then(buf => {
            if (!buf) return
            try {
                if (_playing[url]) this.stop(url)
                const source = ac.createBufferSource()
                source.buffer = buf
                source.loop = !!loop
                const gain = ac.createGain()
                gain.gain.value = (volume == null) ? 1 : volume
                source.connect(gain)
                gain.connect(ac.destination)
                source.start()
                _playing[url] = { source, gain }
            } catch { /* 忽略音频异常 */ }
        })
    },

    // 停止该 url 的播放（用于循环 BGM 或打断音效）。
    stop(url) {
        const n = _playing[url]
        if (!n) return
        try { n.source.stop() } catch { /* 已停止 */ }
        try { n.source.disconnect() } catch { /* ignore */ }
        try { n.gain.disconnect() } catch { /* ignore */ }
        delete _playing[url]
    },

    // 实时调整正在播放实例的音量。
    setVolume(url, volume) {
        const n = _playing[url]
        if (n) n.gain.gain.value = volume
    },
}
