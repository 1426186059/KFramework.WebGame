// 【测试模块 Bench_ByteArrayReturn 的 JS 侧】对比两条“把像素字节交给 C#”的路线性能：
//  A) MemoryView：JS 把字节写入 C# 预分配的共享视图（零拷贝）。
//  B) 直接返回 byte[]：JS 新建 Uint8Array 并 return，C# 封送复制成新 byte[]。
// 实测（1000 次，1KB→256KB）：B 反而更快，且随体积差距扩大（256KB 约快 6 倍）；
// 原因：A 逐字节穿 WASM 内存代理有跨边界开销，B 仅一次批量 memcpy。引擎 GetImageData 即采 B。
// 接线：main.js 里 setModuleImports('bench_bytearrayreturn', ...)。

// A) MemoryView 写入：bytes 是 C# 的 Span<byte> → MemoryView（共享视图），写入即落在托管数组上。
// 同步调用：.NET 在调用期间 pin 住托管数组、调用后即解 pin，故 JS 侧【不】调用 dispose（框架负责）。
export function fillMemoryView(bytes) {
    const a = bytes;            // Uint8Array 视图
    const len = a.byteLength;
    for (let i = 0; i < len; i++) a[i] = (i * 31) & 0xff;
}

// B) 直接返回 byte[]：新建一个 Uint8Array 并返回（C# 收成新 byte[]，运行时复制一次）。
export function returnByteArray(size) {
    const a = new Uint8Array(size);
    for (let i = 0; i < size; i++) a[i] = (i * 31) & 0xff;
    return a;
}
