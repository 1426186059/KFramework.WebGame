// =====================================================================
// 音效层：@pixi/sound（Pixi 官方声音库，本地 vendor/pixi-sound.mjs）
//   - beep(freq, dur, waveType, vol)   合成音（WebAudio，无需外部资源）
//   - load(url)                        预加载 mp3/wav/ogg（可省略，播放时自动加载）
//   - play(url, loop, volume)          播放音频文件
//   - stop(url)                        停止该 url 的播放
//   - setVolume(url, volume)           实时调整音量
// 音频上下文须由用户手势解锁（ensure()），在输入事件中调用。
// =====================================================================

import { sound } from '../vendor/pixi-sound.mjs'

// 合成音走 WebAudio（@pixi/sound 不提供 oscillator 合成音）
let _oscCtx = null
function oscCtx() {
    if (!_oscCtx) {
        const AC = window.AudioContext || window.webkitAudioContext
        if (AC) _oscCtx = new AC()
    }
    if (_oscCtx && _oscCtx.state === 'suspended') _oscCtx.resume()
    return _oscCtx
}

export const audio = {
    init() { sound.removeAll() },

    // 解锁音频：恢复 @pixi/sound 的 AudioContext，并预创建合成音上下文。
    ensure() {
        if (sound.context) {
            const ac = sound.context.audioContext || sound.context
            if (ac && ac.state === 'suspended') ac.resume()
        }
        oscCtx()
    },

    beep(freq, dur, waveType, vol) {
        const ac = oscCtx()
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

    // 预加载音频文件（mp3/wav/ogg）。播放时也会自动加载，可不预调。
    load(url) {
        if (!sound.exists(url)) sound.add(url, { url, preload: true })
    },

    // 播放音频文件。loop 是否循环，volume 0~1。
    // 同 url 重复调用：若正在播则先停止再重播。
    play(url, loop, volume) {
        if (!sound.exists(url)) sound.add(url, { url, preload: true })
        sound.play(url, { loop: !!loop, volume: (volume == null) ? 1 : volume })
    },

    // 停止该 url 的播放（用于循环 BGM 或打断音效）。
    stop(url) { sound.stop(url) },

    // 实时调整正在播放实例的音量（0~1）。
    setVolume(url, volume) { sound.volume(url, volume) },
}
