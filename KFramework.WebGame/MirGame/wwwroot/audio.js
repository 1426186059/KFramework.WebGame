// 程序化音效：全部用 WebAudio 振荡器 + 噪声实时合成，不需要任何音频文件。
// 0=Shoot 1=Explosion 2=Hit 3=Pickup 4=Select 5=GameOver 6=PowerUp

let context = null;
let master = null;
let muted = false;
let noiseBuffer = null;

function ensureContext() {
    if (context) return context;
    const Ctor = window.AudioContext || window.webkitAudioContext;
    if (!Ctor) return null;

    context = new Ctor();
    master = context.createGain();
    master.gain.value = 0.35;
    master.connect(context.destination);
    return context;
}

function getNoiseBuffer() {
    if (noiseBuffer) return noiseBuffer;
    const length = Math.floor(context.sampleRate * 0.6);
    noiseBuffer = context.createBuffer(1, length, context.sampleRate);
    const data = noiseBuffer.getChannelData(0);
    for (let i = 0; i < length; i++) data[i] = Math.random() * 2 - 1;
    return noiseBuffer;
}

export function unlock() {
    const ctx = ensureContext();
    if (ctx && ctx.state === 'suspended') ctx.resume();
}

export function setMuted(value) {
    muted = !!value;
    if (master) master.gain.value = muted ? 0 : 0.35;
}

function tone(type, from, to, duration, volume, delay = 0) {
    const ctx = ensureContext();
    if (!ctx || muted) return;

    const start = ctx.currentTime + delay;
    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();

    oscillator.type = type;
    oscillator.frequency.setValueAtTime(from, start);
    oscillator.frequency.exponentialRampToValueAtTime(Math.max(1, to), start + duration);

    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(volume, start + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);

    oscillator.connect(gain);
    gain.connect(master);
    oscillator.start(start);
    oscillator.stop(start + duration + 0.02);
}

function noise(duration, volume, cutoffFrom, cutoffTo) {
    const ctx = ensureContext();
    if (!ctx || muted) return;

    const start = ctx.currentTime;
    const source = ctx.createBufferSource();
    source.buffer = getNoiseBuffer();

    const filter = ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.setValueAtTime(cutoffFrom, start);
    filter.frequency.exponentialRampToValueAtTime(Math.max(80, cutoffTo), start + duration);

    const gain = ctx.createGain();
    gain.gain.setValueAtTime(volume, start);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);

    source.connect(filter);
    filter.connect(gain);
    gain.connect(master);
    source.start(start);
    source.stop(start + duration);
}

export function play(kind, volume, pitch) {
    if (muted) return;
    const v = Math.max(0, Math.min(1, volume));
    const p = pitch > 0 ? pitch : 1;

    switch (kind | 0) {
        case 0: // 射击：短促下滑方波
            tone('square', 880 * p, 220 * p, 0.10, 0.16 * v);
            break;
        case 1: // 爆炸：低频噪声
            noise(0.45, 0.55 * v, 1800, 90);
            tone('sawtooth', 180 * p, 40 * p, 0.35, 0.14 * v);
            break;
        case 2: // 命中
            tone('triangle', 420 * p, 180 * p, 0.09, 0.18 * v);
            break;
        case 3: // 拾取：上行三音
            tone('sine', 660 * p, 660 * p, 0.07, 0.20 * v, 0);
            tone('sine', 880 * p, 880 * p, 0.07, 0.20 * v, 0.06);
            tone('sine', 1170 * p, 1170 * p, 0.10, 0.18 * v, 0.12);
            break;
        case 4: // 菜单选择
            tone('sine', 520 * p, 780 * p, 0.08, 0.16 * v);
            break;
        case 5: // 游戏结束
            tone('sawtooth', 420 * p, 60 * p, 0.9, 0.22 * v);
            tone('square', 210 * p, 40 * p, 1.0, 0.12 * v, 0.05);
            break;
        case 6: // 强化：上行琶音
            tone('square', 440 * p, 440 * p, 0.08, 0.16 * v, 0);
            tone('square', 587 * p, 587 * p, 0.08, 0.16 * v, 0.07);
            tone('square', 880 * p, 880 * p, 0.14, 0.18 * v, 0.14);
            break;
    }
}
