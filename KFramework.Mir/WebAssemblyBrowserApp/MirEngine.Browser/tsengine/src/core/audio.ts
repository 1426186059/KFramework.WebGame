// mirengine/core/audio.ts
// Web Audio 音效后端。对应 JSBind/BrowserAudio.cs（mir.initAudio / playSound / stopSound / stopAllSounds / setSoundVolume）。
//
// 原版客户端用 NAudio/WaveOut 播声；WASM 下没有原生音频后端，这里统一走 WebAudio：
// URL（已由 C# 侧 ResolveUrl 拼好资源服务器地址）-> 字节 -> 浏览器解码 -> 播放。
// 解码结果按 URL 缓存（同一声效每步都会触发，不能每次重解码）。
import { audio, ensureAudio, host, AudioEntry } from '../shared.js';

// 浏览器自动播放策略：AudioContext 创建后处于 suspended，必须在用户手势里才能出声。
// 游戏里第一次点击/按键就会走到这里。
function resumeAudio(): void {
    const ctx = ensureAudio();
    if (!ctx) return;
    if (ctx.state === 'running') {
        window.removeEventListener('pointerdown', resumeAudio, true);
        window.removeEventListener('mousedown', resumeAudio, true);
        window.removeEventListener('keydown', resumeAudio, true);
        console.log('[audio] AudioContext 已恢复');
    }
}
window.addEventListener('pointerdown', resumeAudio, true);
window.addEventListener('mousedown', resumeAudio, true);
window.addEventListener('keydown', resumeAudio, true);

// 游戏侧音量是 0..100，WebAudio 增益是 0..1
/**
 * 游戏音量 -> WebAudio 增益。
 * @param volume 游戏侧音量 0..100
 * @returns 0..1 增益（非法值按 1 处理）
 */
const toGain = (volume: number): number => {
    const v = Number(volume);
    if (!Number.isFinite(v)) return 1;
    return Math.max(0, Math.min(1, v / 100));
};

const _failed = new Set<string>();

// 取字节：优先命中 host.assets（同步 XHR / 启动预取写入的缓存），否则异步 fetch。
/**
 * 取音效文件字节（带缓存）。
 * @param url 音效地址
 * @returns 字节内容；取不到返回 null
 */
async function loadBytes(url: string): Promise<Uint8Array | null> {
    const cached = host.assets.get(url);
    if (cached) return cached;
    try {
        const r = await fetch(url);
        if (!r.ok) return null;
        const bytes = new Uint8Array(await r.arrayBuffer());
        host.assets.set(url, bytes);
        return bytes;
    } catch (e) {
        return null;
    }
}

// decodeAudioData 同时支持回调与 Promise 两种写法，都注册一遍（重复 resolve 无害）。
/**
 * 解码音频数据。
 * @param ctx AudioContext
 * @param data 音频文件字节（必须是副本：decodeAudioData 会 detach 传入的 ArrayBuffer）
 * @returns 解码后的 AudioBuffer
 */
function decodeAudio(ctx: AudioContext, data: ArrayBuffer): Promise<AudioBuffer> {
    return new Promise<AudioBuffer>((resolve, reject) => {
        const p = ctx.decodeAudioData(data, resolve, reject);
        if (p && typeof p.then === 'function') p.then(resolve, reject);
    });
}

/**
 * 取（必要时下载 + 解码）音效 Buffer，结果按 URL 缓存。
 * @param ctx AudioContext
 * @param url 音效地址
 * @returns AudioBuffer；取不到或解码失败返回 null
 */
async function loadBuffer(ctx: AudioContext, url: string): Promise<AudioBuffer | null> {
    const cachedBuf = audio.buffers.get(url);
    if (cachedBuf) return cachedBuf;

    const bytes = await loadBytes(url);
    if (!bytes || bytes.length === 0) {
        if (!_failed.has(url)) {
            _failed.add(url);
            console.warn('[audio] 音效文件取不到(404?):', url);
        }
        return null;
    }

    let buf: AudioBuffer;
    try {
        // decodeAudioData 会 detach 传入的 ArrayBuffer，必须传副本，
        // 否则会把 host.assets 里缓存的字节清空，同一声效第二次就播不出来了。
        buf = await decodeAudio(ctx, bytes.slice().buffer);
    } catch (e) {
        if (!_failed.has(url)) {
            _failed.add(url);
            console.warn('[audio] 音效解码失败:', url, e);
        }
        return null;
    }

    audio.buffers.set(url, buf);
    return buf;
}

/** 初始化 WebAudio 后端（mir.initAudio）。 */
export const initAudio = (): void => {
    const ctx = ensureAudio();
    console.log('[audio] WebAudio 后端就绪, state =', ctx ? ctx.state : '(浏览器不支持 AudioContext)');
};

/**
 * 播放音效（mir.playSound）。解码是异步的：先返回 id 给游戏层，解码完再真正出声。
 * @param url 音效地址
 * @param volume 音量 0..100
 * @param loop 是否循环
 * @returns 音效句柄 id（用于 stopSound / setSoundVolume）；无 AudioContext 返回 0
 */
export const playSound = (url: string, volume: number, loop: boolean): number => {
    const ctx = ensureAudio();
    if (!ctx) return 0;

    const id = audio.nextId++;
    const entry: AudioEntry = { src: null, gain: null, canceled: false, volume: toGain(volume) };
    audio.active.set(id, entry);

    // 解码是异步的：先把 id 返回给游戏层（stopSound 需要它），解码完再真正出声。
    loadBuffer(ctx, url).then((buf) => {
        if (entry.canceled) return;
        if (!buf) {
            audio.active.delete(id);
            return;
        }

        const src = ctx.createBufferSource();
        src.buffer = buf;
        src.loop = !!loop;

        const gain = ctx.createGain();
        gain.gain.value = entry.volume;
        src.connect(gain).connect(ctx.destination);
        src.onended = () => audio.active.delete(id);

        entry.src = src;
        entry.gain = gain;

        try {
            src.start(0);
        } catch (e) {
            audio.active.delete(id);
        }
    });

    return id;
};

/**
 * 停止指定音效（mir.stopSound）。
 * @param id playSound 返回的句柄
 */
export const stopSound = (id: number): void => {
    const a = audio.active.get(id);
    if (!a) return;
    a.canceled = true;
    if (a.src) { try { a.src.stop(); } catch (e) { } }
    audio.active.delete(id);
};

/** 停止所有正在播放的音效（mir.stopAllSounds）。 */
export const stopAllSounds = (): void => {
    for (const a of audio.active.values()) {
        a.canceled = true;
        if (a.src) { try { a.src.stop(); } catch (e) { } }
    }
    audio.active.clear();
};

/**
 * 调整指定音效音量（mir.setSoundVolume）。
 * @param id playSound 返回的句柄
 * @param volume 新音量 0..100
 */
export const setSoundVolume = (id: number, volume: number): void => {
    const a = audio.active.get(id);
    if (!a) return;
    a.volume = toGain(volume);
    if (a.gain) a.gain.gain.value = a.volume;
};
