// =====================================================================
// 音效层：WebAudio 简单合成（方波/正弦 beep）。
// 音频上下文须由用户手势解锁（ensure()），在输入事件中调用。
// =====================================================================

let _audioCtx = null

export const audio = {
    init() { _audioCtx = null },

    ensure() {
        if (!_audioCtx) {
            const AC = window.AudioContext || window.webkitAudioContext
            if (AC) _audioCtx = new AC()
        }
        if (_audioCtx && _audioCtx.state === 'suspended') _audioCtx.resume()
    },

    beep(freq, dur, waveType, vol) {
        if (!_audioCtx) return
        try {
            const osc = _audioCtx.createOscillator()
            const gain = _audioCtx.createGain()
            osc.type = waveType || 'square'
            osc.frequency.value = freq
            const t = _audioCtx.currentTime
            const v = vol || 0.08
            gain.gain.setValueAtTime(v, t)
            gain.gain.exponentialRampToValueAtTime(0.0001, t + (dur || 0.1))
            osc.connect(gain)
            gain.connect(_audioCtx.destination)
            osc.start(t)
            osc.stop(t + (dur || 0.1) + 0.02)
        } catch { /* 忽略音频异常 */ }
    },
}
