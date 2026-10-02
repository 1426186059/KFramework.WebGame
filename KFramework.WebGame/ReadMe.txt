AI 协作约定（代码注释与命名）
================================

【注释】
- 类（class）或文件头（file header）的注释可以写长：说明整体设计、为什么这么写、
  跨语言（C# ↔ TS）对应关系、关键约束等，越清楚越好。
- 方法（method / 模块函数）的注释顶多一行：一句话说清“做什么 / 关键不变量”，
  不要展开原理、逐个列参数说明、也不要写长篇注意事项。
  - 需要强调的坑（如 MemoryView 必须拷成 Uint8Array、byte[] 复制语义会把字节丢在 JS 副本）
    浓缩进那一行，或只写在实现里的行内注释（//）。
  - 适用范围：库代码（KFramework.MonoGame、KFramework.TSEngine）严格遵守；纯工具/脚本可放宽。

【命名：JS ↔ C# 互操作】
- TS 侧导出的模块函数名 == C# 的 [JSImport] 字符串 == C# JSBind_* 类的静态方法名，
  三者完全一致（PascalCase + Async 后缀）。
  例：C# JSBind_CacheStorage.GetCacheSizeAsync
    ↔ [JSImport("GetCacheSizeAsync", "cachestorage")]
    ↔ TS export async function GetCacheSizeAsync(...)
- 不要给 TS 模块函数加 Caching 前缀（Caching 是 C# 封装类名）；动词直接开头：
  Get / Load / Save / Remove + 对象 + Async。
- C# 的 Caching 封装类只持有“缓存名（string）”，不持有 JS 对象句柄
  （避免句柄泄漏、且能继续走 [JSImport] 的源生成 / AOT）；
  一切调用都经 JSBind_* 的模块函数，由 TS 侧按名池化真正的 Cache 句柄。

【C# 写法】
- 禁止使用表达式体成员（=> 简写）：属性 getter、方法、lambda 之外的 => 一律展开成
  带 { } 的语句体。
  例：
    // 禁止
    public int Count => _n;
    public Task<int> GetAsync() => JSBind.X();
    // 必须写成
    public int Count { get { return _n; } }
    public Task<int> GetAsync() { return JSBind.X(); }
  原因：统一可读性、避免“每次访问是否执行”“是否为同一引用”这类隐含语义被忽略。
