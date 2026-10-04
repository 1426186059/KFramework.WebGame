// 【共享类型】本文件定义 MemoryView_Span / MemoryView_ArraySegment 等共享类型，供引擎各模块（及对应 JSBind）使用；自身不产生独立 JS 文件。
//
// .NET 的 Span<T> / ArraySegment<T> 在 JS 侧都以 MemoryView 的形式传入（不是 TypedArray），但二者语义不同，故保留两个名字：
//   * MemoryView_Span         —— 对应 Span<T>：只在【同步】调用期间有效（无 await），不 pin 托管数组。
//   * MemoryView_ArraySegment —— 对应 ArraySegment<T>：可跨 await 持有，会 pin 托管数组，用完需 dispose 解 pin。
// 选哪个看 C# 侧该 [JSImport] 参数声明的是 Span 还是 ArraySegment（见各 JSBind_*.cs）。
//
// 【这两个名字现在只是官方 IMemoryView 的别名，不再手写接口】
// 底层定义取自 dotnet/runtime 的官方声明（src/dotnet-runtime.d.ts，从源码原样复制）。
// 之所以仍叫两个名字：运行时的 Span / ArraySegment 两个类都 extends MemoryView，
// 类型形状【完全一致】—— 都有 dispose / isDisposed（Span 版的 dispose 只置个标志，因为它没 pin 所以无需解 pin），
// 区别只在语义（能否跨 await）。留两个名字是为了让调用点一眼看出用的是哪一种。
//
// 用法要点（以官方定义为准，与早期口头描述不同）：
//   * MemoryView 既不是 TypedArray、也不是 array-like，【没有 [] 索引器，也没有单元素的 get(i)/set(i,v)】。
//   * 没有 getTypedArray（内部叫 _unsafe_create_view，不对外）。
//   * copyTo(target, sourceOffset?) 只有 2 个参数（无 targetOffset / count）。
//   * set(source, targetOffset?) 2 个参数；source 的构造函数必须与视图元素类型一致，否则抛异常。
//   * slice(start?, end?) 返回的是 TypedArray【副本】（写入不会回写 C# 缓冲）；按索引读单个元素只能先 slice()。
//
// 【为什么用 import('./dotnet-runtime.js') 的内联写法，而不是顶层 import】
// 顶层 import 会让本文件从"全局声明文件"变成模块，MemoryView_Span 就不再是全局类型，
// 各模块里那几十处直接使用会全部失效。内联 import() 只取类型、不改变本文件的全局性。

type MemoryView_Span = import('./dotnet-runtime.js').IMemoryView;
type MemoryView_ArraySegment = import('./dotnet-runtime.js').IMemoryView;

// dotnet 运行时由 _framework 提供：产物里只有 .js，【不带 .d.ts】，所以官方类型放在 src/dotnet-runtime.d.ts。
// 这里把它挂到 '*/dotnet.js' 这个通配模块上，让 main.ts 的
// `import { dotnet } from '../_framework/dotnet.js'` 解析到【真实】的 DotnetHostBuilder，
// 而不是早先手写的一小撮方法 —— 那份只认得 setModuleImports / getAssemblyExports / getConfig / runMain，
// create() 真正返回的 RuntimeAPI 上的 localHeapViewU8 / Module / getConfig 之类全都看不见。
//
// 同样用【内联】import() 取类型：顶层 import 会把本文件变成模块，上面的全局别名就失效了。
declare module '*/dotnet.js' {
    export const dotnet: import('./dotnet-runtime.js').DotnetHostBuilder;
}
