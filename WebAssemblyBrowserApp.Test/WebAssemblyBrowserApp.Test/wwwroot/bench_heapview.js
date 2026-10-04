// 【测试模块 Bench_HeapView 的 JS 侧】"GCHandle pin + 裸地址 → TypedArray"零拷贝方案，
// 声明见 Bench_HeapView/JSBind_HeapView.cs。
//
// 一个测试模块一个 js，不共用其它模块的函数（理由见 bench_cstojs.js）。
// 接线：main.js 里 setModuleImports('bench_heapview', heapView)。
//
// 【本模块最关键的一条前提，来自 Bench_MemoryView 的 ⑨⑩⑪ 实测】
//   .NET 的 BrowserApp 不像 Emscripten 那样把 wasmMemory / HEAPU8 挂在全局 —— 扫全局必然一无所获；
//   唯一可靠的入口是借任意一个 MemoryView 的 _unsafe_create_view().buffer，那才是整块 WASM 线性内存。
//   所以本模块里凡是"要拿 buffer"的函数，都先收一个【引子】Span<byte>（C# 侧传过来就是 MemoryView），
//   从它身上换出 buffer。仍然保留全局扫描，只为作对照：坐实"扫全局就是扫不到"。

const CTORS = {
    Uint8Array: Uint8Array,
    Int8Array: Int8Array,
    Uint16Array: Uint16Array,
    Int16Array: Int16Array,
    Uint32Array: Uint32Array,
    Int32Array: Int32Array,
    Float32Array: Float32Array,
    Float64Array: Float64Array,
};

function getCtor(name) {
    const c = Object.prototype.hasOwnProperty.call(CTORS, name) ? CTORS[name] : null;
    if (!c) throw new Error('Unsupported typed array: ' + name);
    return c;
}

/**
 * 扫全局找 WASM memory.buffer —— 【已被 ⑨ 判定为必然失败的一条路】。
 *
 * 保留它只为作对照：让"必须借道 MemoryView"这个结论有据可依，而不是听说的。
 */
function scanGlobal() {
    const candidates = [
        () => globalThis.Module && globalThis.Module.wasmMemory && globalThis.Module.wasmMemory.buffer,
        () => globalThis.wasmMemory && globalThis.wasmMemory.buffer,
        () => globalThis.dotnet && globalThis.dotnet.wasmMemory && globalThis.dotnet.wasmMemory.buffer,
        () => globalThis.HEAPU8 && globalThis.HEAPU8.buffer,
        () => globalThis.Module && globalThis.Module.HEAPU8 && globalThis.Module.HEAPU8.buffer,
    ];

    for (const get of candidates) {
        try {
            const b = get();
            if (b && b.byteLength > 0) return b;
        } catch (e) {
            // 某个入口抛错不影响继续找下一个
        }
    }
    return null;
}

/**
 * 取 WASM 的 memory.buffer —— 本方案的命门。
 *
 * 主路：借【引子】MemoryView 的 _unsafe_create_view().buffer。按 marshal.ts 的定义它内部是
 *   new Uint8Array(localHeapViewU8().buffer, this._pointer, this._length)
 * 因此 .buffer 就是整块线性内存（⑪ 已实测：用它访问"另一个"被 pin 的数组，C# 侧确实被改写）。
 * 兜底：扫全局。
 */
function acquireBuffer(key) {
    let keyErr = '';

    if (key && typeof key._unsafe_create_view === 'function') {
        try {
            const v = key._unsafe_create_view();
            const b = v && v.buffer;
            if (b && b.byteLength > 0) {
                return { buffer: b, via: '引子 MemoryView._unsafe_create_view().buffer', keyErr: '' };
            }
            keyErr = '引子有 _unsafe_create_view，但取不到有效 .buffer';
        } catch (e) {
            keyErr = '引子取 buffer 抛错：' + (e && e.message ? e.message : String(e));
        }
    } else {
        keyErr = '引子不是 MemoryView（没有 _unsafe_create_view）';
    }

    const b = scanGlobal();
    if (b) return { buffer: b, via: '全局兜底扫描（引子不可用）', keyErr: keyErr };
    return { buffer: null, via: '', keyErr: keyErr };
}

/** ①-a 对照：只扫全局，能不能拿到 buffer。（⑨ 的结论：拿不到。） */
export function probeGlobal() {
    const b = scanGlobal();
    if (!b) return "一个都没有（.NET 不把裸堆视图挂到全局）";
    return "全局扫到 memory.buffer，byteLength=" + b.byteLength;
}

/** ①-b 正解：借引子 MemoryView 取 buffer。 */
export function probeViaKey(key) {
    const got = acquireBuffer(key);
    if (!got.buffer) {
        return "借引子也没拿到：" + (got.keyErr || "未知原因");
    }
    return "拿到 memory.buffer，byteLength=" + got.buffer.byteLength + "，途径=" + got.via;
}

/**
 * ② 附加校验：这个地址到底落不落在这块 buffer 里。
 * 光看"ptr != 0"说明不了任何问题 —— 地址非零但越界，建视图一样会失败，
 * 而那种失败很容易被误读成"零拷贝不成立"。
 */
export function checkRange(key, ptr, byteLength) {
    const got = acquireBuffer(key);
    if (!got.buffer) return "拿不到 buffer，无法校验";
    if (!ptr) return "ptr 为 0（没钉住），无意义";

    const end = ptr + byteLength;
    const inRange = ptr >= 0 && end <= got.buffer.byteLength;
    return "ptr=0x" + ptr.toString(16) +
        "，需 " + byteLength + " 字节，buffer.byteLength=" + got.buffer.byteLength +
        " → " + (inRange ? "落在范围内 ✓" : "越界 ✘（ptr+" + byteLength + " > " + got.buffer.byteLength + "）");
}

/**
 * 建视图。比原来多了三道校验，每一道都是为了【不让失败被误判成"方案不成立"】：
 *   1）ptr 非 0；
 *   2）ptr+字节数 不越界；
 *   3）ptr 按元素宽度对齐（Uint32Array 等要求 byteOffset 是其字节宽度的整数倍，否则 RangeError）。
 */
export function createView(key, ptr, length, typedArrayName) {
    const got = acquireBuffer(key);
    if (!got.buffer) {
        throw new Error('拿不到 WASM memory.buffer：' + (got.keyErr || '引子与全局两条路都失败'));
    }
    if (!ptr) throw new Error('ptr 为 0（.NET 侧没钉住数组，或地址无效）');

    const ctor = getCtor(typedArrayName);
    const bytes = length * ctor.BYTES_PER_ELEMENT;
    const end = ptr + bytes;

    if (ptr < 0 || end > got.buffer.byteLength) {
        throw new Error('地址越界：需要 [' + ptr + ', ' + end + ')，而 buffer 只有 ' +
            got.buffer.byteLength + ' 字节');
    }
    if (ptr % ctor.BYTES_PER_ELEMENT !== 0) {
        throw new Error('地址未按 ' + ctor.BYTES_PER_ELEMENT + ' 字节对齐（ptr=0x' + ptr.toString(16) +
            '），' + typedArrayName + ' 要求 byteOffset 对齐，请改用 Uint8Array 视图');
    }

    return new ctor(got.buffer, ptr, length);
}

/** 拷贝：先建视图再 slice() 出来一份，脱离 WASM 内存（多一次 memcpy，但可长期持有）。 */
export function copyArray(key, ptr, length, typedArrayName) {
    return createView(key, ptr, length, typedArrayName).slice();
}

/**
 * 往视图里写入固定值。
 *
 * 按视图的元素宽度决定怎么写：Uint8Array 按字节截断（& 0xff）；
 * 更宽的元素就写满整个元素 —— 否则 Uint32Array 上"逐字节写"的语义是错的，
 * 会把每个 32 位元素写成同一个小数，看了还以为写成功了。
 */
export function writeView(view, value) {
    try {
        const w = view.BYTES_PER_ELEMENT;
        const isFloat = view instanceof Float32Array || view instanceof Float64Array;
        const v = isFloat ? value : (w === 1 ? (value & 0xff) : value);
        for (let i = 0; i < view.length; i++) view[i] = v;
        return "view.length=" + view.length + "（" + view.constructor.name + "，元素宽 " + w +
            " 字节），已写入 " + v;
    } catch (e) {
        return "写入失败：" + (e && e.message ? e.message : String(e));
    }
}

/** 从视图里读回前 count 个元素。 */
export function readView(view, count) {
    try {
        const n = Math.min(count, view.length);
        const out = [];
        for (let i = 0; i < n; i++) out.push(view[i]);
        return "读到 [" + out.join(",") + "]";
    } catch (e) {
        return "读取失败：" + (e && e.message ? e.message : String(e));
    }
}

/**
 * ⑧ 跨数组验证：用引子的 buffer，去访问【另一个】被 pin 住的数组。
 * 写成功就证明那块 buffer 是整块线性内存，而不是引子自己那一小段。
 */
export function writeOther(key, ptr, length, value) {
    const got = acquireBuffer(key);
    if (!got.buffer) return "拿不到 buffer：" + (got.keyErr || "未知原因");
    if (!ptr) return "ptr 为 0 —— C# 侧没钉住那个数组";

    try {
        const v = new Uint8Array(got.buffer, ptr, length);
        for (let i = 0; i < v.length; i++) v[i] = value & 0xff;
        return "用引子的 buffer + ptr=0x" + ptr.toString(16) + " 建视图成功（视图 byteOffset=" +
            v.byteOffset + "），已写入 0x" + (value & 0xff).toString(16).toUpperCase();
    } catch (e) {
        return "建视图/写入失败：" + (e && e.message ? e.message : String(e));
    }
}

// ---- ⑦ 生存期：memory.grow 会不会让 buffer detach、视图失效 ----
// 这是整套方案唯一的硬伤所在，也是唯一没被实测过的项。
// 做法：先把 buffer 与视图【跨调用】存在这里，再由 C# 去胁迫 WASM 堆增长，最后回来查。
// 判据：detached 的 ArrayBuffer 其 byteLength 变 0，挂在它上面的 TypedArray 的 length 也变 0，
//       此后读写既不抛错也不生效 —— 正是那种"看着跑通了、其实没干活"的坑。

let kept = null; // { buffer, view, bufferLen0 }

/** 存下 buffer 与视图，回报初始状态。 */
export function keepForGrow(key, ptr, length) {
    const got = acquireBuffer(key);
    if (!got.buffer) return "存不下 —— 拿不到 buffer：" + (got.keyErr || "未知原因");
    if (!ptr) return "存不下 —— ptr 为 0，无法建视图";

    try {
        const view = new Uint8Array(got.buffer, ptr, length);
        kept = { buffer: got.buffer, view: view, bufferLen0: got.buffer.byteLength };
        return "已存下：buffer.byteLength=" + got.buffer.byteLength +
            "，view.length=" + view.length + "，byteOffset=" + view.byteOffset;
    } catch (e) {
        return "存不下 —— 建视图失败：" + (e && e.message ? e.message : String(e));
    }
}

/** 只查询、不写入：看存的 buffer / 视图有没有因为堆增长而失效。 */
export function inspectKept() {
    if (!kept) return "没有存过视图";

    const bufLen = kept.buffer.byteLength;
    const detached = bufLen === 0;
    const viewLen = kept.view.length;
    const grew = !detached && bufLen !== kept.bufferLen0;

    return "初始 buffer.byteLength=" + kept.bufferLen0 + "，现在=" + bufLen +
        "，view.length=" + viewLen +
        " → " + (detached
            ? "buffer 已 detach ✘（byteLength 归零，视图随之失效）"
            : grew
                ? "buffer 已增长且未 detach（视图可能指向旧位置，需实写验证）"
                : "buffer 未变，视图仍有效");
}

/** 真正往存的视图里写一次 —— 只有这一步能证明视图到底是活是死。 */
export function writeKept(value) {
    if (!kept) return "没有存过视图";

    const before = kept.view.length;
    try {
        for (let i = 0; i < kept.view.length; i++) kept.view[i] = value & 0xff;
        return "写之前 view.length=" + before + "，写之后 view.length=" + kept.view.length +
            "，已尝试写入 0x" + (value & 0xff).toString(16).toUpperCase();
    } catch (e) {
        return "写入失败：" + (e && e.message ? e.message : String(e));
    }
}

/** 收尾：放掉跨调用持有的引用（本模块的 store 不该活到下一轮）。 */
export function releaseKept() {
    kept = null;
    return "已释放";
}
