// 【共享】可增长的字节缓冲，封装一块复用中的 Uint8Array，供各渲染模块（及后续新增模块）共用。
//
// 【存在的理由已更新 —— 原先那句"这次拷贝免不掉"是错的】
// .NET 侧传进来的 MemoryView_Span 不是 TypedArray、也没有公开的 .buffer，
// 但它的 _unsafe_create_view() 能给出【共享托管内存】的 TypedArray —— 那是零拷贝的，
// 可直接喂给 WebGL / WebGPU（细节见 copyFrom 的注释）。所以本类现在的角色是【兜底】：
//   * 拿得到共享视图 → 零拷贝，根本用不到本缓冲；
//   * 拿不到，或异步用途必须固化一份 JS 自有字节 → 才拷进本缓冲，
//     此时省掉的是"每次 new Uint8Array"的分配，拷贝本身仍在。
//
// 用法：每个用途各持一个实例（矩阵 / 顶点 / 纹理…），【不要混用】。
// 不同用途的数据量级差得远（几十 B vs 数 MB），共用一个实例会互相把对方撑到自己的量级。
//
// 生命周期：只增不减。跑一段时间容量稳定后，分配次数归零。

/**
 * 一块可增长的字节缓冲。
 *
 * 典型用法：
 * ```ts
 * const cache = new ByteCache(2048);
 * const bytes = cache.copyFrom(memoryView);   // 交给 gl.bufferSubData / texImage2D
 * ```
 */
/** 默认容量上限：ushort 最大值 65535。小用途（矩阵等）足够，大用途需显式放宽。 */
const DEFAULT_MAX_CAPACITY = 0xffff;

/** 扩容倍数：写死 2，每次容量翻倍。初始容量是 2 的整数次幂，翻倍后仍是整次幂。 */
const GROWTH_FACTOR = 2;

/**
 * 断言 n 是 2 的整数次幂（1 / 2 / 4 / 8…），不是就抛错。
 * 构造参数写错就该立刻暴露 —— 等到扩容后长度不对再去查，成本高得多。
 */
function assertPowerOfTwo(name: string, n: number): number {
    const v = n | 0;
    if (v <= 0 || (v & (v - 1)) !== 0)
        throw new Error(`[ByteCache] ${name} 必须是 2 的整数次幂（1/2/4/8…），收到 ${n}`);
    return v;
}

/**
 * 把 .NET 传进来的字节（MemoryView 或 TypedArray）取成【共享内存】的 Uint8Array —— 零拷贝。
 *
 * 两条路都能拿到共享视图：
 *   - 已是 TypedArray：直接建字节视图，共享同一段内存。
 *   - MemoryView：_unsafe_create_view() 返回的正是共享托管内存的 TypedArray。
 *     它内部就是 new Uint8Array(localHeapViewU8().buffer, ptr, len)（marshal.ts:481）；
 *     与公开 API runtime.localHeapViewU8() 是同一块内存 —— Bench_RuntimeApi ⑤⑥ 已实测。
 *     它带 _unsafe 前缀属内部方法，故拿不到时本函数返回 null，由调用方自己兜底。
 *
 * ⚠️ 返回的视图【只在当前同步调用期间有效】：别存起来、别跨 await。
 *    堆一增长（memory.grow），底层 buffer 被 detach，视图会【静默失效】
 *    —— 读写既不抛错也不生效，是最难查的一类 bug。
 *    同步用途安全：gl.bufferData / gl.texImage2D / new Blob(...) 这些 API 都是【同步读取】的，
 *    数据在调用返回前就已固化。异步用途（要先 await 才用到字节）必须先自己固化一份。
 *
 * @returns 共享视图；拿不到（或传 null）时返回 null。
 */

// ============================================================ 零拷贝到底生效没有？可观测的统计
//
// 为什么需要这个：按 dotnet/runtime 的 marshal-to-js.ts（Span<byte> → JS 侧 new Span(...)），
// C# 传进来的应当是 MemoryView 实例、不是 TypedArray，于是会走 _unsafe_create_view() 拿到共享内存。
// 但"运行时到底给了什么"只有跑起来才知道 —— 万一哪天 .NET 把它封送成了 TypedArray，
// 下面 isView 分支就会命中，而那份 TypedArray 其实是跨界封送时的【副本】，
// "零拷贝"就名存实亡。这些计数就是照妖镜：
//   shared 高 → 真的零拷贝；
//   isView 高 → 入参已是副本，跨界时就已经拷过一次（此时零拷贝无从谈起）。
const _zc = {
    total: 0,
    shared: 0,      // 拿到共享内存视图（零拷贝）
    isView: 0,      // 入参已是 TypedArray（跨界封送的产物，不是共享内存）
    missView: 0,    // 是 MemoryView，但没有可用的 _unsafe_create_view
    copied: 0,      // 最终退回拷贝的次数（由 ByteCache.copyFrom 记）
    kinds: {} as Record<string, number>,
};

const _warned = new Set<string>();

/** 每种情况只在控制台报一次，避免刷屏。 */
function warnOnce(key: string, text: string): void {
    if (_warned.has(key)) return;
    _warned.add(key);
    console.warn('[ByteCache] ' + text);
}

/**
 * 零拷贝统计，返回一段可读文本（供 C# 侧经已注册的模块取回显示）。
 * 只想看一眼的话，控制台里也有各分支首次命中时的 warn。
 */
export function copyFromStats(): string {
    const kinds = Object.entries(_zc.kinds)
        .map(([k, v]) => k + '×' + v)
        .join('，') || '（无）';
    return '零拷贝 ' + _zc.shared + ' / 入参已是副本 ' + _zc.isView +
        ' / 无共享视图 ' + _zc.missView + ' / 退回拷贝 ' + _zc.copied +
        '（共 ' + _zc.total + ' 次；入参类型：' + kinds + '）';
}

export function sharedBytesOf(view: MemoryView_Span | ArrayBufferView | null): Uint8Array | null {
    if (view == null) return null;

    _zc.total++;
    const kind = (view as unknown as { constructor?: { name?: string } })?.constructor?.name ?? typeof view;
    _zc.kinds[kind] = (_zc.kinds[kind] ?? 0) + 1;

    // 已是 JS 侧的 TypedArray：直接建一个共享同一段内存的字节视图。
    // ⚠️ 但要注意：走到这里说明入参【不是】MemoryView —— 它多半是跨界封送出来的副本，
    // 那次拷贝在跨界时已经发生，本函数省不掉。
    if (ArrayBuffer.isView(view)) {
        _zc.isView++;
        warnOnce('isView:' + kind,
            `零拷贝【未】生效：入参已是 TypedArray（${kind}）—— 它是跨界封送的副本，不是共享内存。` +
            `按 marshal-to-js.ts，Span<byte> 应以 Span 实例传入才对，请查该调用点的 [JSMarshalAs]。`);
        return new Uint8Array(view.buffer, view.byteOffset, view.byteLength);
    }

    const memory = view as MemoryView_Span;

    // MemoryView：要共享内存的那份视图。_unsafe_create_view 不在官方 IMemoryView 类型里，故强转。
    const createSharedView = (memory as unknown as {
        _unsafe_create_view?: () => ArrayBufferView;
    })._unsafe_create_view;

    if (typeof createSharedView === 'function') {
        try {
            const shared = createSharedView.call(memory);
            // 长度对得上才敢用：对不上说明拿到的不是本视图的共享内存
            if (shared && shared.byteLength === memory.byteLength) {
                _zc.shared++;
                warnOnce('shared:' + kind,
                    `零拷贝生效 ✓：入参 ${kind} → _unsafe_create_view() 拿到共享托管内存的视图，无 memcpy。`);
                return new Uint8Array(shared.buffer, shared.byteOffset, shared.byteLength);
            }
        } catch {
            // 拿不到就返回 null，交给调用方兜底
        }
    }

    _zc.missView++;
    warnOnce('miss:' + kind,
        `零拷贝【未】生效：入参 ${kind} 上找不到可用的 _unsafe_create_view，将退回拷贝。`);
    return null;
}

export class ByteCache {
    private buffer: Uint8Array;
    private readonly initialCapacity: number;
    private readonly maxCapacity: number;

    /**
     * @param initialCapacity 初始容量（字节），必须是 2 的整数次幂。
     *                        不从"刚好够本次"起步，省掉头几次连续扩容。
     * @param maxCapacity     容量上限（字节），默认 65535（ushort 最大值）。
     *                        这是个安全阀：防止单次异常请求把缓冲撑到失控。
     *                        注意顶点 / 纹理这类大块数据远超此值，必须显式传更大的上限。
     */
    constructor(initialCapacity = 2048, maxCapacity = DEFAULT_MAX_CAPACITY) {
        // 初始容量必须是 2 的整数次幂：这样按 GROWTH_FACTOR 翻倍下去也始终是整次幂，便于对齐与预估。
        // 断言放在钳制之前 —— 上限不会低于初始容量，故断言通过的值就是最终采用的初始值。
        const initial = assertPowerOfTwo('initialCapacity', initialCapacity);
        const max = Math.max(1, maxCapacity | 0);
        this.maxCapacity = Math.max(max, initial); // 上限不低于初始容量，否则自相矛盾
        this.initialCapacity = initial;
        this.buffer = new Uint8Array(this.initialCapacity);
    }

    /** 当前容量（字节）。 */
    get capacity(): number {
        return this.buffer.length;
    }

    /** 容量上限（字节）。 */
    get limit(): number {
        return this.maxCapacity;
    }

    /** 当前缓冲。注意扩容后会换新对象，别长期持有这个引用。 */
    get bytes(): Uint8Array {
        return this.buffer;
    }

    /**
     * 确保容量 ≥ byteLength，不够则按 GROWTH_FACTOR（2）翻倍，而不是只扩到刚好等于需求
     * —— 否则请求量在阈值附近抖动时会每次都重新分配。
     * @returns 当前缓冲（可能已换新）。
     */
    ensure(byteLength: number): Uint8Array {
        if (byteLength > this.maxCapacity)
            throw new Error(
                `[ByteCache] 需要 ${byteLength} 字节，超出容量上限 ${this.maxCapacity}。` +
                '要么给这个用途的实例传更大的 maxCapacity，要么别拿小实例接大数据。');
        if (this.buffer.length >= byteLength) return this.buffer;
        const grown = this.buffer.length * GROWTH_FACTOR;   // 容量恒为整次幂，直接乘即可
        this.buffer = new Uint8Array(
            Math.min(this.maxCapacity, Math.max(byteLength, grown, this.initialCapacity)));
        return this.buffer;
    }

    /**
     * 把 MemoryView / TypedArray 取成一份可直接交给图形 API 的 Uint8Array。
     *
     * 【零拷贝优先 —— 两种情况都能拿到共享内存的视图，一次 memcpy 都没有】
     * - 已是 TypedArray（Uint8Array / Float32Array…）：直接建字节视图，共享同一段内存。
     * - MemoryView：_unsafe_create_view() 返回的正是【共享托管内存】的 TypedArray。
     *   它内部就是 new Uint8Array(localHeapViewU8().buffer, ptr, len)
     *   （见 dotnet-runtime.d.ts 旁的 reference 与 marshal.ts:481；
     *    Bench_RuntimeApi ⑤⑥ 已实测它与公开 API runtime.localHeapViewU8() 是同一块内存）。
     *   带 _unsafe 前缀属内部方法，故拿不到时才退回下面的拷贝。
     *
     * ⚠️ 零拷贝返回的视图【只在当前同步调用期间有效】：
     *   别存起来、别跨 await —— 堆一增长（memory.grow），底层 buffer 被 detach，
     *   视图会【静默失效】（读写既不抛错也不生效）。
     *   本类现有的调用点（bufferData / bufferSubData / texImage2D / texSubImage2D /
     *   compressedTexImage2D / writeBuffer / uploadTexture / uniformMatrix4fv）
     *   都是同步提交给图形 API 的，故安全。
     *   异步用途（音频解码、写 CacheStorage / IndexedDB、图片解码）【不能】走本方法，
     *   必须拷一份 JS 自有的字节 —— 否则数据会在 await 之后失效。
     *
     * @param view MemoryView_Span（.NET Span<byte>）或任意 TypedArray；传 null 直接返回 null。
     */
    copyFrom(view: MemoryView_Span | ArrayBufferView | null): Uint8Array | null {
        if (view == null) return null;

        // 先要共享视图（零拷贝）—— 这是首选路径
        const shared = sharedBytesOf(view);
        if (shared) return shared;

        // 兜底：拷进本缓冲（一次 memcpy）。走到这里说明拿不到共享视图，
        // 此时本类仍有用 —— 省掉的是"每次 new Uint8Array"的分配。
        _zc.copied++;
        const memory = view as MemoryView_Span;
        const buf = this.ensure(memory.byteLength);

        if (typeof memory.copyTo === 'function') {
            memory.copyTo(buf); // 视图 → JS 缓冲（读出 C# 侧的字节）
        } else {
            // 兜底：没有 copyTo 时退回 slice()，它本身就是一份副本
            const sliced = memory.slice();
            buf.set(new Uint8Array(sliced.buffer, sliced.byteOffset, sliced.byteLength));
        }

        // 缓冲可能比本次数据大，只返回用到的这一段
        return buf.subarray(0, memory.byteLength);
    }
}

// 暴露到全局，方便控制台实时查看零拷贝累计次数。
// 注：sharedBytesOf 里的 warnOnce 每种情况只报一次（避免刷屏），想知道累计跑了多少次
// 零拷贝 / 退回拷贝，在控制台调：__byteCacheStats()
(globalThis as Record<string, unknown>).__byteCacheStats = copyFromStats;
