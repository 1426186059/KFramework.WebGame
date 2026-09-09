// .NET 的 Span<T> 在 JS 侧以 MemoryView 的形式传入（不是 TypedArray），
// 因此这里给出它的最小可用类型描述，供引擎各模块共享。
interface MemoryView {
    readonly byteLength: number;
    /** 元素个数（按视图的元素类型计算，不是字节数） */
    readonly length: number;
    copyTo(target: ArrayBufferView): void;
    slice(start?: number, end?: number): ArrayBufferView;
    set(source: ArrayBufferView, targetOffset?: number): void;
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
