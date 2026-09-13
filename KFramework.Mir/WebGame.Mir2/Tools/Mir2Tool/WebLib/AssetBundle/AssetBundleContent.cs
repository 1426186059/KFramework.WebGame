namespace WebLib;

/// <summary>
/// 单个 .web.lib 包内的清单（包内 manifest.json）。加载时由 <see cref="AssetBundle.Content"/> 暴露。
/// 对齐 Unity 内部 AssetBundle 的 manifest（本库把它作为可读数据暴露给引擎）。
/// </summary>
/// <param name="Format">固定 "web.lib"</param>
/// <param name="Version">清单结构版本</param>
/// <param name="Name">逻辑名，如 3-1</param>
/// <param name="Entries">资源索引</param>
public sealed record AssetBundleContent(string Format, int Version, string Name, List<AssetBundleEntry> Entries);
