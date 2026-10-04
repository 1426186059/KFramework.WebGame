// 【共享】可增长的字节缓冲，封装一块复用中的 Uint8Array，供各渲染模块（及后续新增模块）共用。
//
// 存在的理由：.NET 侧传进来的 MemoryView_Span 不是 TypedArray，而且【没有 .buffer】，
// 要交给 WebGL / WebGPU 前必须先 copyTo 或 slice 出一份 JS 侧字节。这次拷贝免不掉，
// 但"每次调用都 new Uint8Array"的分配可以免 —— 本类就是干这个的。
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
function assertPowerOfTwo(name, n) {
    const v = n | 0;
    if (v <= 0 || (v & (v - 1)) !== 0)
        throw new Error(`[ByteCache] ${name} 必须是 2 的整数次幂（1/2/4/8…），收到 ${n}`);
    return v;
}
export class ByteCache {
    buffer;
    initialCapacity;
    maxCapacity;
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
    get capacity() {
        return this.buffer.length;
    }
    /** 容量上限（字节）。 */
    get limit() {
        return this.maxCapacity;
    }
    /** 当前缓冲。注意扩容后会换新对象，别长期持有这个引用。 */
    get bytes() {
        return this.buffer;
    }
    /**
     * 确保容量 ≥ byteLength，不够则按 GROWTH_FACTOR（2）翻倍，而不是只扩到刚好等于需求
     * —— 否则请求量在阈值附近抖动时会每次都重新分配。
     * @returns 当前缓冲（可能已换新）。
     */
    ensure(byteLength) {
        if (byteLength > this.maxCapacity)
            throw new Error(`[ByteCache] 需要 ${byteLength} 字节，超出容量上限 ${this.maxCapacity}。` +
                '要么给这个用途的实例传更大的 maxCapacity，要么别拿小实例接大数据。');
        if (this.buffer.length >= byteLength)
            return this.buffer;
        const grown = this.buffer.length * GROWTH_FACTOR; // 容量恒为整次幂，直接乘即可
        this.buffer = new Uint8Array(Math.min(this.maxCapacity, Math.max(byteLength, grown, this.initialCapacity)));
        return this.buffer;
    }
    /**
     * 把 MemoryView / TypedArray 取成一份可直接交给图形 API 的 Uint8Array。
     * - 已是 TypedArray（Uint8Array / Float32Array…）：零拷贝地转成字节视图直返，不占用本缓冲。
     * - MemoryView：拷进本缓冲，返回只含本次数据的 subarray 视图。
     *
     * @param view MemoryView_Span（.NET Span<byte>）或任意 TypedArray；传 null 直接返回 null。
     */
    copyFrom(view) {
        if (view == null)
            return null;
        // 已是 JS 侧的 TypedArray：直接建一个共享同一段内存的字节视图，零拷贝
        if (ArrayBuffer.isView(view)) {
            return new Uint8Array(view.buffer, view.byteOffset, view.byteLength);
        }
        const memory = view;
        const buf = this.ensure(memory.byteLength);
        if (typeof memory.copyTo === 'function') {
            memory.copyTo(buf); // 视图 → JS 缓冲（读出 C# 侧的字节）
        }
        else {
            // 兜底：没有 copyTo 时退回 slice()，它本身就是一份副本
            const sliced = memory.slice();
            buf.set(new Uint8Array(sliced.buffer, sliced.byteOffset, sliced.byteLength));
        }
        // 缓冲可能比本次数据大，只返回用到的这一段
        return buf.subarray(0, memory.byteLength);
    }
}
