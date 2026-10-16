namespace MapExtract2;

/// <summary>
/// 把地图单元格里的“库编号”映射到磁盘/运行时 Lib 相对路径（不含扩展名）。
/// 完全复刻客户端 <c>Mir2/MirGraphics/MLibrary.cs</c> 里 <c>Libraries.MapLibs</c> 的静态构造，
/// 保证蒸馏出的 Lib 落在与运行时 <c>MLibrary</c> 请求完全一致的逻辑路径上
/// （例如 <c>Data/Map/WemadeMir2/Tiles</c>，运行时会被补成 .Lib）。
/// 返回 null 表示该编号没有配置对应 Lib（客户端里也是占位空 MLibrary）。
/// </summary>
public static class MapLibPathTable
{
    /// <summary>
    /// 取得库相对路径（不含扩展名，使用 '/'）。例如 <c>Data/Map/WemadeMir2/Tiles</c>。
    /// </summary>
    public static string? GetRelPath(int index)
    {
        if (index < 0) return null;
        string? p = null;

        if (index == 0) p = "Map/WemadeMir2/Tiles";
        else if (index == 1) p = "Map/WemadeMir2/Smtiles";
        else if (index == 2) p = "Map/WemadeMir2/Objects";
        else if (index >= 3 && index <= 28)
            p = "Map/WemadeMir2/Objects" + (index - 1);   // 复刻客户端: N -> Objects(N-1)，即 3..28 -> Objects2..Objects27
        else if (index == 90) p = "Map/WemadeMir2/Objects_32bit";
        else if (index == 100) p = "Map/ShandaMir2/Tiles";
        else if (index >= 101 && index <= 109)
            p = "Map/ShandaMir2/Tiles" + (index - 100 + 1); // 101..109 -> Tiles2..Tiles10
        else if (index == 110) p = "Map/ShandaMir2/SmTiles";
        else if (index >= 111 && index <= 119)
            p = "Map/ShandaMir2/SmTiles" + (index - 110 + 1); // 111..119 -> SmTiles2..SmTiles10
        else if (index == 120) p = "Map/ShandaMir2/Objects";
        else if (index >= 121 && index <= 150)
            p = "Map/ShandaMir2/Objects" + (index - 120 + 1); // 121..150 -> Objects2..Objects31
        else if (index == 190) p = "Map/ShandaMir2/AniTiles1"; // 瓦片动画固定走这里
        else if (index >= 200 && index <= 274)              // WemadeMir3（有 5 种地形）
        {
            string[] states = { "", "wood/", "sand/", "snow/", "forest/" };
            int k = (index - 200) / 15;
            int r = (index - 200) % 15;
            var s = states[k];
            p = "Map/WemadeMir3/" + s + r switch
            {
                0 => "Tilesc",
                1 => "Tiles30c",
                2 => "Tiles5c",
                3 => "Smtilesc",
                4 => "Housesc",
                5 => "Cliffsc",
                6 => "Dungeonsc",
                7 => "Innersc",
                8 => "Furnituresc",
                9 => "Wallsc",
                10 => "smObjectsc",
                11 => "Animationsc",
                12 => "Object1c",
                13 => "Object2c",
                _ => "Unknown"
            };
        }
        else if (index >= 300 && index <= 374)              // ShandaMir3（有 5 种地形）
        {
            string[] states = { "", "wood", "sand", "snow", "forest" };
            int k = (index - 300) / 15;
            int r = (index - 300) % 15;
            var s = states[k];
            p = "Map/ShandaMir3/" + r switch
            {
                0 => "Tilesc" + s,
                1 => "Tiles30c" + s,
                2 => "Tiles5c" + s,
                3 => "Smtilesc" + s,
                4 => "Housesc" + s,
                5 => "Cliffsc" + s,
                6 => "Dungeonsc" + s,
                7 => "Innersc" + s,
                8 => "Furnituresc" + s,
                9 => "Wallsc" + s,
                10 => "smObjectsc" + s,
                11 => "Animationsc" + s,
                12 => "Object1c" + s,
                13 => "Object2c" + s,
                _ => "Unknown"
            };
        }

        // 注意：客户端以 Settings.DataPath(=".\Data\") 为基，请求的是 Data/Map/.../X.Lib，
        // 源文件也在 <SourceRoot>/Data/Map/... 下，故这里统一补 "Data/" 前缀。
        return p == "Unknown" ? null : "Data/" + p;
    }
}
