// 【测试模块 Bench_CsToJs 的 JS 侧】C# → JS 的跨界实现，声明见 Bench_CsToJs/JSBind_CsToJs.cs。
//
// 一个测试模块一个 js，且<b>不共用任何其它模块的函数</b>：连数据源与计时都各写一份。
// 代价是几行重复，换来的是"改这个模块不会动到别人" —— 基准测试里这个代价值得付。
//
// 接线：main.js 里 setModuleImports('bench_cstojs', csToJs)。

// ---- 本模块自己的数据源（与别的模块各写一份，互不 import）----

// 只此一块源缓冲：按最大档开足 2048 字节，初始化时填一次，之后全程复用。
// 不每次 new（那样把"生成数据"的成本反复计入），也不建 Map 按长度存好几份（没必要 —— 各档共用这一块）。
const SRC_CAP = 2048;
const src = new Uint8Array(SRC_CAP);
for (let i = 0; i < SRC_CAP; i++) src[i] = i & 0xff;

/**
 * 取源缓冲前 n 字节（subarray 是<b>视图</b>，不复制）。
 * 真正的拷贝发生在交付那一刻：view.set() 写进 C# 缓冲，或过界时运行时复制成托管 byte[]。
 */
const bytesOf = (n) => src.subarray(0, n);

// ---- C# 会调过来的函数（与 JSBind_CsToJs.cs 的 [JSImport] 一一对应）----

/** 调用空的 JS 函数 —— 纯跨界下限（无载荷）。 */
export function noop() { }

export function echoInt(v) { return v; }

export function echoString(s) { return s; }

/**
 * MemoryView：C# 提供缓冲，JS 直接往里写（零拷贝路径）。
 *
 * 【坑】MemoryView 不是 TypedArray：没有 [] 索引器，也不接受单元素 set(i, v)，
 * 只有 set(源, 偏移) / copyTo / slice。早先写成 view[i] = ... —— 那只是给 JS 对象挂普通属性，
 * 托管内存一个字节都没被写，计时却照常出数，属于"看着有结果、其实没干活"。
 */
export function fillSpan(view, n) {
    view.set(bytesOf(n), 0);   // 每次先拷出一份新数据，再写进 C# 的缓冲
    return n;
}

/** byte[]：JS 建好数组交回 C#（每次都要分配；过界时运行时再复制一次）。 */
export function makeArray(n) {
    return bytesOf(n);
}

/** C# 把 byte[] 传进来（运行时已复制成副本），这里只回报长度。 */
export function sendBytes(bytes) {
    return bytes.length;
}
