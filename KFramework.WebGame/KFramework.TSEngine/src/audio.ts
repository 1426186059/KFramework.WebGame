// 【依赖 C#】由 KFramework.MonoGame.JSBind_Audio 经 [JSImport(module: "audio")] 调用；编译产物 audio.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
// 音频引擎：两条能力并存。
// 1) 合成音效：WebAudio 振荡器 + 噪声实时合成，零资源，适合原型（见 playSynth）。
// 2) 真实音频：wav/mp3/ogg 经 decodeAudioData 解码成 AudioBuffer，再按实例播放（见 loadAudio / createInstance）。
//    实例模型对齐 MonoGame 的 SoundEffectInstance：一个缓冲可对应多个实例，各自控制播放/音量/音高/声相。

let MASTER_VOLUME = 0.35;

let context: AudioContext | null = null;
let master: GainNode | null = null;
let muted = false;
let noiseBuffer: AudioBuffer | null = null;

const buffers = new Map<number, AudioBuffer>();   // 缓冲 handle → AudioBuffer
const instances = new Map<number, Instance>();     // 实例 id → 播放实例
let nextInstance = 1;

interface Instance {
    buffer: AudioBuffer;
    source: AudioBufferSourceNode | null;
    gain: GainNode;
    panner: StereoPannerNode;
    loop: boolean;
    volume: number;
    pitch: number;
    pan: number;
    startedAt: number;   // ctx.currentTime，用于 pause 时计算偏移
    offset: number;      // 暂停后的播放位置（秒）
    playing: boolean;
    paused: boolean;
}

function ensureContext(): AudioContext | null {
    if (context) return context;

    const Ctor: typeof AudioContext | undefined =
        window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) return null;

    context = new Ctor();
    master = context.createGain();
    master.gain.value = muted ? 0 : MASTER_VOLUME;
    master.connect(context.destination);
    return context;
}

// 浏览器自动播放策略：AudioContext 必须在用户手势后才能从 suspended 变为 running。
// 否则即使调用了 source.start()，声音也完全静默（"声音听不见" 的最常见原因）。
// 这里在引擎层注册一次性手势监听，首次 pointerdown/keydown/touchstart 时自动 resume，
// 任何示例都无需手动调用 unlock()，也不依赖游戏逻辑。
let autoUnlockAttached = false;
function attachAutoUnlock(): void {
    if (autoUnlockAttached) return;
    autoUnlockAttached = true;

    const resume = (): void => {
        const ctx = ensureContext();
        if (ctx && ctx.state === 'suspended') void ctx.resume();
    };

    window.addEventListener('pointerdown', resume);
    window.addEventListener('keydown', resume);
    window.addEventListener('touchstart', resume);
}
attachAutoUnlock();

function getNoiseBuffer(ctx: AudioContext): AudioBuffer {
    if (noiseBuffer) return noiseBuffer;

    const length = Math.floor(ctx.sampleRate * 0.6);
    noiseBuffer = ctx.createBuffer(1, length, ctx.sampleRate);
    const data = noiseBuffer.getChannelData(0);
    for (let i = 0; i < length; i++) data[i] = Math.random() * 2 - 1;
    return noiseBuffer;
}

export function unlock(): void {
    const ctx = ensureContext();
    if (ctx && ctx.state === 'suspended') void ctx.resume();
}

export function setMuted(value: boolean): void {
    muted = !!value;
    if (master) master.gain.value = muted ? 0 : MASTER_VOLUME;
}

export function setMasterVolume(value: number): void {
    MASTER_VOLUME = Math.max(0, Math.min(1, value));
    if (master) master.gain.value = muted ? 0 : MASTER_VOLUME;
}

// ===== 合成音效 =====

export function playTone(
    type: OscillatorType, from: number, to: number,
    duration: number, volume: number, delay = 0,
): void {
    const ctx = ensureContext();
    if (!ctx || muted || !master) return;

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

export function playNoise(duration: number, volume: number, cutoffFrom: number, cutoffTo: number): void {
    const ctx = ensureContext();
    if (!ctx || muted || !master) return;

    const start = ctx.currentTime;
    const source = ctx.createBufferSource();
    source.buffer = getNoiseBuffer(ctx);

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

// ===== 真实音频缓冲 =====

export function loadAudio(handle: number, data: Uint8Array, _mime: string): void {
    const ctx = ensureContext();
    if (!ctx) return;

    // decodeAudioData 会 detach 底层 ArrayBuffer，必须把托管内存拷贝出来。
    const copy = data.slice();
    ctx.decodeAudioData(
        copy.buffer,
        (buf) => { buffers.set(handle, buf); },
        (err) => {
            // 不能再静默：解码失败是"声音听不见"的常见原因，必须能定位到
            console.error('[audio] decodeAudioData 失败, handle=' + handle, err);
        },
    );
}

export function isLoaded(handle: number): boolean {
    return buffers.has(handle);
}

export function getDuration(handle: number): number {
    const b = buffers.get(handle);
    return b ? b.duration : 0;
}

export function releaseBuffer(handle: number): void {
    buffers.delete(handle);
}

// ===== 播放实例 =====

export function createInstance(handle: number): number {
    const buffer = buffers.get(handle);
    if (!buffer || !context) return 0;

    const id = nextInstance++;
    instances.set(id, {
        buffer,
        source: null,
        gain: context.createGain(),
        panner: context.createStereoPanner(),
        loop: false,
        volume: 1,
        pitch: 1,
        pan: 0,
        startedAt: 0,
        offset: 0,
        playing: false,
        paused: false,
    });
    return id;
}

function startSource(inst: Instance, fromOffset: number, id: number): void {
    if (!context || !master) return;

    // 防御性 resume：若在用户手势内触发播放但自动解锁尚未生效，这里再补一次。
    if (context.state === 'suspended') void context.resume();

    const src = context.createBufferSource();
    src.buffer = inst.buffer;
    src.loop = inst.loop;
    src.playbackRate.value = inst.pitch;

    inst.gain.gain.value = inst.volume;
    inst.panner.pan.value = inst.pan;

    src.connect(inst.panner);
    inst.panner.connect(inst.gain);
    inst.gain.connect(master);

    src.start(0, fromOffset);
    inst.source = src;
    inst.startedAt = context.currentTime;
    inst.offset = fromOffset;
    inst.playing = true;
    inst.paused = false;

    src.onended = () => {
        if (inst.loop || inst.paused) return;
        inst.playing = false;
        inst.source = null;
        instances.delete(id);   // 一次性播放（非循环、非暂停）结束后自动清理实例
    };
}

export function playInstance(id: number, volume: number, pitch: number, pan: number, loop: boolean): void {
    const inst = instances.get(id);
    if (!inst || !context) return;

    // 若正在播，先停旧源再起新源（AudioBufferSourceNode 一次性）
    if (inst.source) {
        inst.source.onended = null;
        try { inst.source.stop(); } catch { /* 已停止 */ }
        inst.source = null;
    }

    inst.loop = loop;
    inst.volume = volume;
    inst.pitch = pitch;
    inst.pan = pan;
    startSource(inst, inst.paused ? inst.offset : 0, id);
}

export function stopInstance(id: number): void {
    const inst = instances.get(id);
    if (!inst) return;
    inst.paused = false;
    inst.offset = 0;
    if (inst.source) {
        inst.source.onended = null;
        try { inst.source.stop(); } catch { /* 已停止 */ }
        inst.source = null;
    }
    inst.playing = false;
}

export function pauseInstance(id: number): void {
    const inst = instances.get(id);
    if (!inst || !inst.source || !context) return;
    inst.offset += context.currentTime - inst.startedAt;
    try { inst.source.stop(); } catch { /* 已停止 */ }
    inst.source = null;
    inst.paused = true;
    inst.playing = false;
}

export function resumeInstance(id: number): void {
    const inst = instances.get(id);
    if (!inst || !inst.paused) return;
    startSource(inst, inst.offset, id);
}

export function setInstanceVolume(id: number, volume: number): void {
    const inst = instances.get(id);
    if (!inst) return;
    inst.volume = volume;
    if (inst.source) inst.gain.gain.value = volume;
}

export function setInstancePitch(id: number, pitch: number): void {
    const inst = instances.get(id);
    if (!inst) return;
    inst.pitch = pitch;
    if (inst.source) inst.source.playbackRate.value = pitch;
}

export function setInstancePan(id: number, pan: number): void {
    const inst = instances.get(id);
    if (!inst) return;
    inst.pan = pan;
    if (inst.source) inst.panner.pan.value = pan;
}

export function setInstanceLoop(id: number, loop: boolean): void {
    const inst = instances.get(id);
    if (!inst) return;
    inst.loop = loop;
    if (inst.source) inst.source.loop = loop;
}

export function isInstancePlaying(id: number): boolean {
    const inst = instances.get(id);
    return !!inst && inst.playing;
}

export function releaseInstance(id: number): void {
    const inst = instances.get(id);
    if (!inst) return;
    if (inst.source) {
        inst.source.onended = null;
        try { inst.source.stop(); } catch { /* 已停止 */ }
    }
    instances.delete(id);
}
