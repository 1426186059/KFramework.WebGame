// 【测试模块 Bench_MemoryView 的 JS 侧】MemoryView 各种写法的探测实现，
// 声明见 Bench_MemoryView/JSBind_MemoryView.cs（C# 传进来的是 Span<byte> → MemoryView）。
//
// 一个测试模块一个 js，不共用其它模块的函数（理由见 bench_cstojs.js）。
// 接线：main.js 里 setModuleImports('bench_memoryview', memoryView)。
//
// 本模块【不计时】：每种写法只跑一次，把"JS 侧看到了什么"回报成字符串即可。
// 真正的判据在 C# 侧 —— 它回读自己的数组，看字节到底有没有被写。
// 只报"跑通了"而字节没动，就是那种最坑的"看着有结果、其实没干活"。

/**
 * 写法① view.set(src, offset) —— 官方写入路径（JS → 托管内存，零拷贝）。
 * 注意 set 的第二参是 targetOffset（写进视图的位置），不是源偏移。
 */
export function probeSet(view) {
    const src = new Uint8Array(view.byteLength);
    for (let i = 0; i < src.length; i++) src[i] = 0xf0 + i;
    view.set(src, 0);
    return "set(" + src.length + "B, offset=0) 已调用，写入 0xF0+i";
}

/** 写法② view.copyTo(target) —— 官方读出路径（托管内存 → JS 的 TypedArray）。 */
export function probeCopyTo(view) {
    const target = new Uint8Array(view.byteLength);
    view.copyTo(target);
    return "copyTo(target) 读到 [" + Array.from(target).join(",") + "]";
}

/**
 * 写法③ view.slice() —— 争议点：副本还是共享视图？
 * 这里改一下返回值：若它是视图，C# 回读会看到变化；若是副本，C# 缓冲纹丝不动。
 */
export function probeSlice(view) {
    const copy = view.slice();
    const type = copy && copy.constructor ? copy.constructor.name : typeof copy;
    copy[0] = 0xaa; // 改返回值
    return "slice() 类型=" + type + "，已把返回值的 [0] 改成 0xAA";
}

/** 写法④ 两个尺寸属性。 */
export function probeMeta(view) {
    return "length=" + view.length + "，byteLength=" + view.byteLength + "，typeof=" + typeof view;
}

/**
 * 写法⑤ 索引读 / 索引写 —— 传闻"没有索引器"。
 * 若真没有，view[0] = v 只是给这个 JS 对象挂了个普通属性，托管内存一个字节都不会变。
 */
export function probeIndex(view) {
    const read = view[0];
    view[0] = 0xbb; // 尝试索引写
    return "view[0] 读到 " + read + "；写入 0xBB 后再读回 " + view[0];
}

/** 写法⑥ view.buffer —— TypedArray 的常规零拷贝入口，看它存不存在。 */
export function probeBuffer(view) {
    return "view.buffer = " + (view.buffer === undefined ? "undefined" : String(view.buffer));
}

/**
 * 写法⑧ 视图能不能【跨调用】持有 —— 分两步：本次存下来，下一次调用再取出来写。
 *
 * 按 src/types.d.ts 的约定：Span 的 MemoryView 只在【同步调用期间】有效（不 pin 托管数组），
 * ArraySegment 的则 pin 住、可跨 await（用完需 dispose）。
 * 若"存下来再写"失败，就说明视图随调用结束失效了 —— 想长期持有这份数据【只能拷出来】，
 * 零拷贝到此为止。
 *
 * 为什么不用 await：本工程的 [JSImport] 源生成不支持 Task<T> 返回值（SYSLIB1072），
 * 而"跨一次调用"同样能验证生命周期，还不必引入异步。
 */
let _stored = null;

export function probeStore(view) {
    _stored = view;
    return "已把视图存进模块级变量（仅留引用，不做任何拷贝）";
}

export function probeUseStored() {
    if (_stored === null) return "没有存过视图";
    try {
        const src = new Uint8Array([0xdd]);
        _stored.set(src, 0); // 存下来的视图还写不写得动
        return "取出上次存的视图，set() 成功";
    } catch (e) {
        return "取出上次存的视图，set() 失败：" + (e && e.message ? e.message : String(e));
    }
}

/**
 * 写法⑨ JS 侧能不能直接碰到 WASM 线性内存（Emscripten 的 HEAPU8 那种"零拷贝视图"）。
 *
 * 有一种说法是"直接 new Uint8Array(wasmMemory.buffer, ptr, len) 就是零拷贝"。
 * 那套做法依赖运行时把堆视图挂在全局 —— Emscripten 会，【.NET 的 WASM 运行时不会】。
 * 实测全局里到底有没有这些入口：没有的话，"零拷贝访问 WASM 内存"这条路在本工程里就不成立，
 * 能用的只有 MemoryView 给的那几个方法。
 */
export function probeHeap() {
    const found = [];
    for (const k of ['HEAPU8', 'HEAP32', 'Module', 'wasmMemory', 'memory']) {
        if (typeof globalThis[k] !== 'undefined') found.push(k);
    }
    return "全局入口：" + (found.length > 0 ? found.join(", ") : "一个都没有（.NET 不暴露裸堆视图）");
}

/**
 * ⑩ _unsafe_create_view() 返回值的身份细节。
 *
 * ⑦ 已证明"写入会回写 C#"，但还不知道它返回的到底是个什么：是不是真的 Uint8Array、有没有 .buffer。
 * 按 reference/MemoryView.ts，它内部是
 *   new Uint8Array(localHeapViewU8().buffer, this._pointer, this._length)
 * —— 那么 .buffer 就应当是【WASM 线性内存】的那块 ArrayBuffer。这条要是成立，HeapView 方案里最难的
 * getWasmMemoryBuffer() 就有了正解：不用扫全局，从一个 MemoryView 身上取即可。
 */
export function probeUnsafeDetail(view) {
    const fn = view._unsafe_create_view;
    if (typeof fn !== 'function') return "_unsafe_create_view 不是函数（不存在）";
    try {
        const v = fn.call(view);
        const v2 = fn.call(view); // 调第二次，看是不是同一个对象
        return [
            '构造函数=' + (v && v.constructor ? v.constructor.name : typeof v),
            'instanceof Uint8Array=' + (v instanceof Uint8Array),
            'length=' + v.length,
            'byteLength=' + v.byteLength,
            'byteOffset=' + v.byteOffset,
            '有 .buffer=' + (v.buffer !== undefined),
            'buffer.byteLength=' + (v.buffer ? v.buffer.byteLength : 0),
            '两次调用同一对象=' + (v === v2),
            '两次调用同一 buffer=' + (v.buffer === v2.buffer),
        ].join('，');
    } catch (e) {
        return "抛错：" + (e && e.message ? e.message : String(e));
    }
}

/**
 * ⑪ 灵魂测试：借 MemoryView 拿到 WASM buffer，再去访问【另一个】被 pin 住的 .NET 数组。
 *
 * 做法：C# 传一个"引子"MemoryView（内容无关紧要），外加另一个数组的 pin 地址 + 长度。
 * 这里用引子的 _unsafe_create_view().buffer 配上那个地址建视图 ——
 * 若写入能让 C# 的【另一个】数组变化，就证明：
 *   1）那个 .buffer 确实是 WASM 线性内存（而不只是引子自己那一小段）；
 *   2）"扫全局找 wasmMemory" 的难题可以绕开：有任意一个 MemoryView 就能取到整块线性内存；
 *   3）于是 HeapView 那套"pin + 裸地址"完全可行，且不再依赖 getWasmMemoryBuffer() 的兜底扫描。
 */
export function probeHeapBridge(view, ptr, length) {
    const fn = view._unsafe_create_view;
    if (typeof fn !== 'function') return "没有 _unsafe_create_view，无法取 buffer";

    let buffer;
    try {
        buffer = fn.call(view).buffer;
    } catch (e) {
        return "取 buffer 失败：" + (e && e.message ? e.message : String(e));
    }
    if (!buffer) return "视图没有 .buffer";
    if (!ptr) return "ptr 为 0 —— C# 侧没钉住数组（该运行时可能不支持 GCHandleType.Pinned）";

    try {
        const v = new Uint8Array(buffer, ptr, length);
        for (let i = 0; i < v.length; i++) v[i] = 0x7e;
        return "用「引子视图的 buffer」+ ptr 建视图成功（buffer.byteLength=" + buffer.byteLength +
            "，视图 byteOffset=" + v.byteOffset + "），已逐个写入 0x7E";
    } catch (e) {
        return "建视图/写入失败：" + (e && e.message ? e.message : String(e));
    }
}

/**
 * 写法⑦ view._unsafe_create_view() —— 运行时内部方法。
 * 按 reference/MemoryView.ts 的定义，它才是真正返回"共享托管内存视图"的那个，
 * 但带 _unsafe 前缀、不对外。实测能不能调、写入会不会回写 C#。
 */
export function probeUnsafe(view) {
    const fn = view._unsafe_create_view;
    if (typeof fn !== 'function') return "_unsafe_create_view 不是函数（不存在）";
    try {
        const v = fn.call(view);
        const type = v && v.constructor ? v.constructor.name : typeof v;
        v[0] = 0xcc; // 写它：若是共享视图，C# 回读会看到
        return "_unsafe_create_view() 返回 " + type + "，已把 [0] 改成 0xCC";
    } catch (e) {
        return "调用抛错：" + (e && e.message ? e.message : String(e));
    }
}
