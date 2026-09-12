namespace MirClient.Assets;

/// <summary>
/// .map 里的库编号 → 实际 .Zl 文件。
/// 与 Zircon 的 Libraries.KROrder（LibraryCore/Libraries.cs:384）保持一致。
/// </summary>
public static class KrOrder
{
    /// <summary>255 表示该层无图。</summary>
    public const int NoImage = 255;

    /// <summary>Tilesc 是底层地面，在 DrawObjects 中被显式跳过（改由背景层绘制）。</summary>
    public const int Tilesc = 0;

    private static readonly Dictionary<int, string> Names = new()
    {
        [0] = "Tilesc", [1] = "Tiles30c", [2] = "Tiles5c", [3] = "SmTilesc",
        [4] = "Housesc", [5] = "Cliffsc", [6] = "Dungeonsc", [7] = "Innersc",
        [8] = "Furnituresc", [9] = "Wallsc", [10] = "SmObjectsc", [11] = "Animationsc",
        [12] = "Object1c", [13] = "Object2c",
        [15] = "Wood_Tilesc", [16] = "Wood_Tiles30c", [17] = "Wood_Tiles5c", [18] = "Wood_SmTilesc",
        [19] = "Wood_Housesc", [20] = "Wood_Cliffsc", [21] = "Wood_Dungeonsc", [22] = "Wood_Innersc",
        [23] = "Wood_Furnituresc", [24] = "Wood_Wallsc", [25] = "Wood_SmObjectsc", [26] = "Wood_Animationsc",
        [30] = "Sand_Tilesc", [31] = "Sand_Tiles30c", [32] = "Sand_Tiles5c", [33] = "Sand_SmTilesc",
        [34] = "Sand_Housesc", [35] = "Sand_Cliffsc", [36] = "Sand_Dungeonsc", [37] = "Sand_Innersc",
        [38] = "Sand_Furnituresc", [39] = "Sand_Wallsc", [40] = "Sand_SmObjectsc", [41] = "Sand_Animationsc",
        [45] = "Snow_Tilesc", [46] = "Snow_Tiles30c", [47] = "Snow_Tiles5c", [48] = "Snow_SmTilesc",
        [49] = "Snow_Housesc", [50] = "Snow_Cliffsc", [51] = "Snow_Dungeonsc", [52] = "Snow_Innersc",
        [53] = "Snow_Furnituresc", [54] = "Snow_Wallsc", [55] = "Snow_SmObjectsc", [56] = "Snow_Animationsc",
        [60] = "Forest_Tilesc", [61] = "Forest_Tiles30c", [62] = "Forest_Tiles5c", [63] = "Forest_SmTilesc",
        [64] = "Forest_Housesc", [65] = "Forest_Cliffsc", [66] = "Forest_Dungeonsc", [67] = "Forest_Innersc",
        [68] = "Forest_Furnituresc", [69] = "Forest_Wallsc", [70] = "Forest_SmObjectsc", [71] = "Forest_Animationsc",
    };

    public static string? GetName(int fileId) => Names.TryGetValue(fileId, out string? n) ? n : null;

    /// <summary>
    /// 返回该库相对于 Data 目录的路径。子目录（Wood/Sand/Snow/Forest）下的
    /// 实际文件名是不带前缀的，例如 Wood_Furnituresc → Map Data/Wood/Furnituresc.Zl。
    /// </summary>
    public static string? GetRelativePath(int fileId)
    {
        if (!Names.TryGetValue(fileId, out string? name))
            return null;

        foreach (string prefix in new[] { "Wood_", "Sand_", "Snow_", "Forest_" })
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return $"Map Data/{prefix.TrimEnd('_')}/{name[prefix.Length..]}.Zl";
        }

        return $"Map Data/{name}.Zl";
    }

    /// <summary>该层是否需要绘制（排除无图与 Tilesc）。</summary>
    public static bool IsDrawable(int fileId) => fileId != NoImage && fileId != Tilesc && Names.ContainsKey(fileId);
}
