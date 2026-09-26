// 【共享类型】本文件定义 MemoryView_Span / MemoryView_ArraySegment 等共享类型，供引擎各模块（及对应 JSBind）使用；自身不产生独立 JS 文件。
// .NET 的 Span<T> / ArraySegment<T> 在 JS 侧都以 MemoryView 的形式传入（不是 TypedArray），但二者语义不同，故拆成两个接口：
//   * MemoryView_Span        —— 对应 Span<T>：只在【同步】调用期间有效（无 await），不 pin 托管数组，因此没有 dispose。
//   * MemoryView_ArraySegment —— 对应 ArraySegment<T>：可跨 await 持有，会 pin 托管数组，用完需 dispose 解 pin。
// 选哪个看 C# 侧该 [JSImport] 参数声明的是 Span 还是 ArraySegment（见各 JSBind_*.cs）。

// 对应 .NET Span<T>：同步调用期间有效，无 dispose。
interface MemoryView_Span {
    readonly byteLength: number;
    /** 元素个数（按视图的元素类型计算，不是字节数） */
    readonly length: number;
    copyTo(target: ArrayBufferView): void;
    slice(start?: number, end?: number): ArrayBufferView;
    set(source: ArrayBufferView, targetOffset?: number): void;
}

// 对应 .NET ArraySegment<T>：可跨 await 持有，用完 dispose 解 pin 托管数组。
interface MemoryView_ArraySegment {
    readonly byteLength: number;
    /** 元素个数（按视图的元素类型计算，不是字节数） */
    readonly length: number;
    copyTo(target: ArrayBufferView): void;
    slice(start?: number, end?: number): ArrayBufferView;
    set(source: ArrayBufferView, targetOffset?: number): void;
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
