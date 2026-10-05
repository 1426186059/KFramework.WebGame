using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_ByteArrayReturn 模块专属</b>的互操作绑定，对应 wwwroot/bench_bytearrayreturn.js。
/// 对比两条“把像素字节交回 C#”的路线：A) JS 写入 C# 预分配的 MemoryView（零拷贝）；B) JS 直接 return byte[]（C# 同步收成数组）。
/// </summary>
public static partial class JSBind_ByteArrayReturn
{
    // A) MemoryView 写入：C# 把预分配的缓冲以 Span + MemoryView 交给 JS，JS 直接写入共享视图（零拷贝）。
    [JSImport("fillMemoryView", "bench_bytearrayreturn")]
    public static partial void FillMemoryView([JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);

    // B) 直接返回 byte[]：JS 新建 Uint8Array 并 return，C# 同步收成 byte[]（运行时复制一次）。
    [JSImport("returnByteArray", "bench_bytearrayreturn")]
    public static partial byte[] ReturnByteArray(int size);
}
