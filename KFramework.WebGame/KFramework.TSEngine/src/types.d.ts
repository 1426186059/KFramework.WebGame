// 【共享类型】本文件定义 MemoryView_Span / MemoryView_ArraySegment 等共享类型，供引擎各模块（及对应 JSBind）使用；自身不产生独立 JS 文件。
// .NET 的 Span<T> / ArraySegment<T> 在 JS 侧都以 MemoryView 的形式传入（不是 TypedArray），但二者语义不同，故拆成两个接口：
//   * MemoryView_Span        —— 对应 Span<T>：只在【同步】调用期间有效（无 await），不 pin 托管数组，因此没有 dispose。
//   * MemoryView_ArraySegment —— 对应 ArraySegment<T>：可跨 await 持有，会 pin 托管数组，用完需 dispose 解 pin。
// 选哪个看 C# 侧该 [JSImport] 参数声明的是 Span 还是 ArraySegment（见各 JSBind_*.cs）。

// 对应 .NET Span<T>：同步调用期间有效，无 dispose。
// 底层即 .NET 运行时 (System.Runtime.InteropServices.JavaScript.MemoryView) 暴露给 JS 的对象，API 如下。
interface MemoryView_Span {
    /** 字节数 */
    readonly byteLength: number;
    /** 元素个数（按视图的元素类型计算，不是字节数） */
    readonly length: number;

    // 把【本视图】从 sourceOffset 起的 count 个元素，拷进 JS 的 target（从 targetOffset 开始）。
    // 方向：视图 → target（读出，用来把 C# 缓冲的数据搬到 JS）。4 个参数。
    copyTo(target: ArrayBufferView, targetOffset?: number, sourceOffset?: number, count?: number): void;
    // 把 source（整段）写入【本视图】，从 targetOffset（元素偏移）开始。
    // 方向：source → 视图（写入，用来把 JS 数据写进 C# 的 Span/缓冲）。只有 2 个参数，不能指定 source 偏移/长度（需要则先 source.subarray 切片）。
    set(source: ArrayBufferView, targetOffset?: number): void;
    // 返回本视图 [start, end) 的【独立副本】（JS 自己的 ArrayBufferView，不再关联 C# 缓冲）。
    slice(start?: number, end?: number): ArrayBufferView;
    // 把本视图的某段直接包装成对应类型的 TypedArray（零拷贝视图，仍指向 C# 缓冲）。type 如 "Uint8"/"Int8"/"Int32"/"Float32"/"Float64" 等。
    getTypedArray(type: string, start?: number, end?: number): ArrayBufferView;
}

// 对应 .NET ArraySegment<T>：可跨 await 持有，用完 dispose 解 pin 托管数组。
// 数据传输 API 与 Span 完全一致（同样基于 .NET MemoryView），只是多了跨 await 的生命周期与 dispose。
interface MemoryView_ArraySegment {
    readonly byteLength: number;
    /** 元素个数（按视图的元素类型计算，不是字节数） */
    readonly length: number;
    copyTo(target: ArrayBufferView, targetOffset?: number, sourceOffset?: number, count?: number): void;
    slice(start?: number, end?: number): ArrayBufferView;
    set(source: ArrayBufferView, targetOffset?: number): void;
    getTypedArray(type: string, start?: number, end?: number): ArrayBufferView;
    /** 解 pin 托管数组并释放代理（仅 ArraySegment 建出的视图有，Span 视图没有、也不需要）。 */
    dispose?(): void;
}

// dotnet 运行时由 _framework 提供，没有类型定义，这里补一个宽松声明。
declare module '*/dotnet.js' {
    interface DotnetModuleImports {
        [key: string]: unknown;
    }

    interface DotnetExports {
        [key: string]: unknown;
    }

    interface DotnetInstance {
        withApplicationArguments(...args: string[]): DotnetInstance;
        create(): Promise<{
            setModuleImports(moduleName: string, imports: Record<string, unknown>): void;
            getAssemblyExports(assemblyName: string): Promise<DotnetExports>;
            getConfig(): { mainAssemblyName: string; [key: string]: unknown };
            runMain(): Promise<void>;
        }>;
    }

    export const dotnet: DotnetInstance;
}
