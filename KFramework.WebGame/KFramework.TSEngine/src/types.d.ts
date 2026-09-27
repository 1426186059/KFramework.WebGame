// 【共享类型】本文件定义 MemoryView_Span / MemoryView_ArraySegment 等共享类型，供引擎各模块（及对应 JSBind）使用；自身不产生独立 JS 文件。
// .NET 的 Span<T> / ArraySegment<T> 在 JS 侧都以 MemoryView 的形式传入（不是 TypedArray），但二者语义不同，故拆成两个接口：
//   * MemoryView_Span        —— 对应 Span<T>：只在【同步】调用期间有效（无 await），不 pin 托管数组，因此没有 dispose。
//   * MemoryView_ArraySegment —— 对应 ArraySegment<T>：可跨 await 持有，会 pin 托管数组，用完需 dispose 解 pin。
// 选哪个看 C# 侧该 [JSImport] 参数声明的是 Span 还是 ArraySegment（见各 JSBind_*.cs）。

// 对应 .NET Span<T>：同步调用期间有效，无 dispose。
// 底层即 .NET WASM 运行时的 MemoryView（来自 dotnet/runtime src/mono/browser/runtime/marshal.ts）暴露给 JS 的对象，API 如下。
// 权威定义见 KFramework.TSEngine/reference/MemoryView.ts。
//
// 注意：MemoryView 既不是 TypedArray、也不是 array-like，【没有 [] 索引器，也没有单元素的 get(i)/set(i,v)】，
// 也没有 getTypedArray（那是内部 _unsafe_create_view，不对外）。
// 按索引读单个元素：用 slice() 取得 TypedArray【副本】后下标访问（副本写入不会回写 C#）。
interface MemoryView_Span {
    /** 字节数 */
    readonly byteLength: number;
    /** 元素个数（按视图的元素类型计算，不是字节数） */
    readonly length: number;

    // 把 source（TypedArray）写入【本视图】，从 targetOffset（元素偏移）开始。只有 2 个参数。
    // 强约束：source 的构造函数必须与视图元素类型一致（Byte 视图→Uint8Array，Int32→Int32Array…），否则抛异常。
    set(source: ArrayBufferView, targetOffset?: number): void;
    // 把【本视图】从 sourceOffset 起到末尾的整段拷进 target（TypedArray）。只有 2 个参数（无 targetOffset / count）；
    // target 须有足够长度容纳剩余元素。方向：视图 → target（读出，把 C# 缓冲数据搬到 JS）。
    copyTo(target: ArrayBufferView, sourceOffset?: number): void;
    // 返回本视图 [start, end) 的 TypedArray【副本】（元素类型与视图一致；是副本，写入不会回写 C# 缓冲）。可下标读。
    slice(start?: number, end?: number): ArrayBufferView;
}

// 对应 .NET ArraySegment<T>：可跨 await 持有，会 pin 托管数组，用完 dispose 解 pin。
// 数据传输 API 与 Span 完全一致（同样基于 .NET MemoryView），只是多了跨 await 的生命周期与 dispose。
interface MemoryView_ArraySegment {
    readonly byteLength: number;
    /** 元素个数（按视图的元素类型计算，不是字节数） */
    readonly length: number;
    set(source: ArrayBufferView, targetOffset?: number): void;
    copyTo(target: ArrayBufferView, sourceOffset?: number): void;
    slice(start?: number, end?: number): ArrayBufferView;
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
