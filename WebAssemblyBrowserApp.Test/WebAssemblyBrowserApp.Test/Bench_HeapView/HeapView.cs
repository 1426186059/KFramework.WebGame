using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// 零拷贝视图方案：用 <see cref="GCHandle"/> 把 .NET 数组钉在 WASM 线性内存上，
/// 再把「内存地址 + 长度」交给 JS，由 JS 直接建出指向同一块内存的 TypedArray。
/// <para>
/// 与运行时给的 MemoryView 的区别：MemoryView 只是一个<b>包装对象</b>（只有 set / copyTo / slice，
/// 拿不到裸地址，且 slice 是副本）；本方案交的是<b>裸地址</b>，JS 侧建的是真正的共享视图，
/// 读写都不经过拷贝。
/// </para>
/// <para>
/// 【buffer 从哪来 —— 本类最容易踩空的一处】
/// 建视图需要 WASM 的 <c>memory.buffer</c>，而 .NET 的 BrowserApp 不像 Emscripten 那样把它挂在全局
/// （Bench_MemoryView 的 ⑨ 已实测：扫全局一个入口都没有）。
/// 正解是借任意一个 MemoryView 的 <c>_unsafe_create_view().buffer</c>（⑩⑪ 已实测：那就是整块线性内存）。
/// 所以本类内置一个 <see cref="Key"/> 引子，每次建视图时一并传给 JS，由 JS 从它身上换出 buffer。
/// </para>
/// </summary>
/// <remarks>
/// 本类<b>只服务 Bench_HeapView 模块</b>，与 Bench_MemoryView 各写一份、互不共用
/// （理由见 Bench_CsToJs/JSBind_CsToJs.cs：共用会让"改一个模块"牵动全部）。
/// </remarks>
public sealed class HeapView<T> : IDisposable where T : unmanaged
{
    /// <summary>
    /// 引子：一个 1 字节的托管数组，内容无关紧要。
    /// 它以 <c>Span&lt;byte&gt;</c> 跨界传过去就是个 MemoryView，JS 侧从它身上取 WASM 的 memory.buffer。
    /// 之所以要"引子"，是因为 .NET 不暴露裸堆入口，而 buffer 又必须有个来处。
    /// </summary>
    private static readonly byte[] s_key = new byte[1];

    private GCHandle _handle;
    private bool _disposed;

    /// <summary>被钉住的数组。</summary>
    public T[] Array { get; }

    /// <summary>元素个数。</summary>
    public int Length => Array.Length;

    /// <summary>这块内存的字节数（元素个数 × 元素宽度）—— JS 侧做越界校验需要它。</summary>
    public int ByteLength => Length * Unsafe.SizeOf<T>();

    /// <summary>
    /// 钉住后拿到的内存地址。<b>0 表示不可用</b>（未钉住、已释放，或运行时尚未初始化 WASM 内存）。
    /// </summary>
    public nint Pointer => _disposed || !_handle.IsAllocated ? 0 : _handle.AddrOfPinnedObject();

    /// <summary>是否已经成功钉住（地址非 0）。</summary>
    public bool IsPinned => Pointer != 0;

    /// <summary>取引子（跨界传过去就是一个 MemoryView），供需要 buffer 的调用使用。</summary>
    public static Span<byte> Key => s_key;

    public HeapView(T[] array)
    {
        Array = array ?? throw new ArgumentNullException(nameof(array));

        // 固定数组，阻止 GC 移动它 —— 这是零拷贝的前提。
        // 注意：Pinned 对 T[] 有效的前提是 T 为 unmanaged（由 where 约束保证）。
        _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
    }

    /// <summary>
    ///TypedArray 视图类型的元素宽度（字节）。1/2/4/8 之外的名字一律拒绝，
    /// 免得拼错的字符串一路传到 JS 才炸。
    /// </summary>
    public static int ElemSizeOf(string typedArrayName) => typedArrayName switch
    {
        "Uint8Array" or "Int8Array" => 1,
        "Uint16Array" or "Int16Array" => 2,
        "Uint32Array" or "Int32Array" or "Float32Array" => 4,
        "Float64Array" => 8,
        _ => throw new ArgumentException(
            "不支持的 TypedArray 视图类型：" + typedArrayName, nameof(typedArrayName)),
    };

    /// <summary>
    /// 零拷贝：返回指向 .NET 数组内存的 JS TypedArray 视图（同一块内存，读写都不拷贝）。
    /// <para>
    /// 要求视图类型的元素宽度与 <typeparamref name="T"/> 一致：
    /// 拿 <c>Uint32Array</c> 去看一个 <c>byte[]</c>，视图长度会被当成"32 位元素个数"，
    /// 直接覆盖到数组之外去。
    /// </para>
    /// <para>
    /// ⚠️ 生存期是最大风险：WASM 内存一旦增长（memory.grow），底层 ArrayBuffer 会被 detach，
    /// 视图立即失效。所以它<b>只能在一次同步调用内用完</b> —— 别存字段、别跨 await、别跨帧。
    /// </para>
    /// </summary>
    public JSObject As(string typedArrayName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireWidthMatch(typedArrayName);
        return JSBind_HeapView.CreateView(Key, Pointer, Length, typedArrayName);
    }

    /// <summary>
    /// 拷贝：返回一份独立的 JS TypedArray（多一次 memcpy，但不受 .NET 堆增长影响，可长期持有）。
    /// 当 <see cref="As"/> 因生存期问题用不了时，用它是安全的兜底。
    /// </summary>
    public JSObject To(string typedArrayName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequireWidthMatch(typedArrayName);
        return JSBind_HeapView.CopyArray(Key, Pointer, Length, typedArrayName);
    }

    /// <summary>视图类型宽度必须与 T 一致，否则视图会按错的步长覆盖内存。</summary>
    private void RequireWidthMatch(string typedArrayName)
    {
        int want = ElemSizeOf(typedArrayName);
        int actual = Unsafe.SizeOf<T>();
        if (want != actual)
        {
            throw new ArgumentException(
                "视图类型 " + typedArrayName + " 的元素宽 " + want + " 字节，与 " + typeof(T).Name +
                " 的 " + actual + " 字节不一致 —— 宽度不符会让视图按错的步长覆盖内存，" +
                "请改用与 " + typeof(T).Name + " 等宽的视图类型",
                nameof(typedArrayName));
        }
    }

    /// <summary>
    /// 释放 pin。<b>注意：这一刻之后，此前建出的所有视图都成了悬空引用</b> ——
    /// JS 侧的对象还在，但它指向的内存随时可能被 GC 复用。视图务必在 Dispose 之前用完。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle.IsAllocated) _handle.Free();
    }
}
