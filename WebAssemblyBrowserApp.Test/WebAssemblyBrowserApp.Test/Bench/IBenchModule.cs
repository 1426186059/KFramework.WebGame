using System.Threading.Tasks;

/// <summary>
/// 一个基准测试模块。每个模块独占一个 .cs 文件，只依赖 <see cref="BenchKit"/>，
/// 因此新增测试不必改动既有文件 —— 在总纲里登记一次即可。
/// </summary>
public interface IBenchModule
{
    /// <summary>显示在总纲上的名字。</summary>
    string Name { get; }

    /// <summary>总纲上的一句话说明（测的是什么、为什么值得测）。</summary>
    string Summary { get; }

    /// <summary>
    /// 独立页面的文件名（不含扩展名），例如 <c>reflection</c>。
    /// 总纲据此生成跳转链接，对应页面加载后也据此自动运行本模块 —— 一个模块一个 HTML 页面。
    /// </summary>
    string Page { get; }

    /// <summary>运行本模块，返回要插入结果区的 HTML 片段。</summary>
    Task<string> RunAsync();
}
