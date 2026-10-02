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

    /// <summary>运行本模块，返回要插入结果区的 HTML 片段。</summary>
    Task<string> RunAsync();
}
