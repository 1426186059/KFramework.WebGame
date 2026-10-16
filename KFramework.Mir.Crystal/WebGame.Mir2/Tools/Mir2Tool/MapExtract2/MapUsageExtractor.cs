using Mir.Map;

namespace MapExtract2;

/// <summary>
/// 解析一张地图，统计它“实际用到”的 (库编号, 图索引) 集合。
/// 索引提取逻辑逐条对齐客户端 <c>GameScene.cs</c> 的绘制代码，
/// 并对动画/门/瓦片动画按运行时会访问到的“索引区间”做放宽，
/// 确保蒸馏出的 Lib 不缺帧（缺帧只会变马赛克，多包含了无害）。
/// </summary>
public static class MapUsageExtractor
{
    /// <returns>库编号 -> 用到的图索引集合（已排序去重）</returns>
    public static Dictionary<int, SortedSet<int>> Extract(MapReader mr, int doorFrameSafety)
    {
        var used = new Dictionary<int, SortedSet<int>>();

        int w = mr.Width;
        int h = mr.Height;
        var cells = mr.MapCells;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = cells[x][y];
                if (c == null) continue;

                // ---- 背景层 (BackIndex / BackImage) ----
                // 注意：必须与客户端 PreloadMapLibraries 保持一致——只要库索引被设置(>=0)就登记该库，
                // 不能先看 BackImage 是否非零。否则“索引已设但本格无图(BackImage=0 或 0x8000 标志)”的格子
                // 会被漏掉，导致该 Lib 不被蒸馏，运行时懒加载即 404（[Mir] 资源缺失，成批崩溃/马赛克）。
                if (c.BackIndex >= 0)
                {
                    EnsureLib(used, c.BackIndex);
                    uint raw = (uint)c.BackImage & 0x1FFFFFFFu;
                    if (raw != 0)
                    {
                        int idx = (int)raw - 1;
                        if (idx >= 0) Add(used, c.BackIndex, idx);
                    }
                }

                // ---- 中层 (MiddleIndex / MiddleImage) ----
                // 同样：索引一设就登记该库（与客户端 PreloadMapLibraries 一致）。
                if (c.MiddleIndex >= 0)
                {
                    EnsureLib(used, c.MiddleIndex);
                    int idx = (int)c.MiddleImage - 1;
                    if (idx >= 0)
                    {
                        Add(used, c.MiddleIndex, idx);
                        int anim = c.MiddleAnimationFrame; // 0 = 无动画
                        if (anim > 0) AddRange(used, c.MiddleIndex, idx, idx + anim);
                    }
                }

                // ---- 前层 (FrontIndex / FrontImage)，客户端跳过 200 ----
                // 同样：索引一设就登记该库（与客户端 PreloadMapLibraries 一致）。
                if (c.FrontIndex >= 0 && c.FrontIndex != 200)
                {
                    EnsureLib(used, c.FrontIndex);
                    uint raw = (uint)c.FrontImage & 0x7FFFu;
                    if (raw != 0)
                    {
                        int idx = (int)raw - 1;
                        if (idx >= 0)
                        {
                            Add(used, c.FrontIndex, idx);
                            int anim = c.FrontAnimationFrame & 0x7F;   // 低位为帧数，高位为混合标记
                            if (anim > 0) AddRange(used, c.FrontIndex, idx, idx + anim);

                            // 门动画：frontIndex += (ImageIndex+1) * DoorOffset
                            if (c.DoorIndex > 0 && c.DoorOffset > 0)
                            {
                                int doorMax = (c.DoorOffset + 1) * c.DoorOffset
                                            + doorFrameSafety * (c.DoorOffset + 1);
                                AddRange(used, c.FrontIndex, idx, idx + doorMax);
                            }
                        }
                    }
                }

                // ---- 瓦片动画（固定走库 190 = AniTiles1）----
                if (c.TileAnimationFrames > 0)
                {
                    int baseIdx = c.TileAnimationImage - 1;
                    if (baseIdx >= 0)
                    {
                        int off = c.TileAnimationOffset ^ 0x2000;
                        int anim = c.TileAnimationFrames;
                        int top = baseIdx + off * (anim - 1);
                        AddRange(used, 190, baseIdx, top);
                    }
                }
            }
        }

        return used;
    }

    private static void Add(Dictionary<int, SortedSet<int>> used, int lib, int idx)
    {
        if (!used.TryGetValue(lib, out var set))
        {
            set = new SortedSet<int>();
            used[lib] = set;
        }
        set.Add(idx);
    }

    private static void AddRange(Dictionary<int, SortedSet<int>> used, int lib, int from, int to)
    {
        if (to < from) (from, to) = (to, from);
        var set = used.TryGetValue(lib, out var s) ? s : (used[lib] = new SortedSet<int>());
        for (int i = from; i <= to; i++) set.Add(i);
    }

    // 仅登记某个库索引（确保 used 中包含该库），不写入具体图像编号。
    // 用于“索引已设但本格无图”的情况：客户端 PreloadMapLibraries 仍会按索引加载该库，
    // 故蒸馏时必须产出该 Lib（即便全是占位图），否则运行时懒加载即 404。
    private static void EnsureLib(Dictionary<int, SortedSet<int>> used, int lib)
    {
        if (!used.ContainsKey(lib)) used[lib] = new SortedSet<int>();
    }
}
