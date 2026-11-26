using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.MirObjects;
using KFramework.MonoGame;

/// <summary>
/// 后台预载所有任务（待接取 + 进行中）目标传送地点的地图瓦片。
///
/// 瓦片最终经 Mir2OriginResource.GetTextureAsync 的全局 _texCache 常驻，
/// 玩家随后点任务行传送过去时，MapControl 取同一张瓦片直接命中缓存，不再逐张下载
/// （否则刚落地时地板会按 512×256 一张张从 CDN 拉，出现空白 / 马赛克）。
///
/// 目标地图的 img / 像素宽高来自 0config map 表，与 MapReader.MapImg 完全同源，
/// 保证预载 key（img:index）与渲染 key 一致。
/// </summary>
public static class QuestMapTilePrefetcher
{
    // key = img:index，避免多个任务目标重复加载同一张瓦片
    private static readonly HashSet<string> _prefetched = new HashSet<string>(StringComparer.Ordinal);
    private static readonly object _lock = new object();

    /// <summary>遍历 CurrentQuests + OfferedQuests 的所有目标，后台批量预载。</summary>
    public static void PrefetchAll()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await PrefetchAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PrintTool.Log("[Map] 任务目标瓦片预载失败: " + ex.Message);
            }
        });
    }

    private static async Task PrefetchAllAsync()
    {
        if (MapObject.User == null)
        {
            return;
        }

        // 快照，避免遍历时网络包正在增删任务列表
        List<ClientQuestProgress> quests = new List<ClientQuestProgress>();
        quests.AddRange(MapObject.User.CurrentQuests.ToArray());
        quests.AddRange(MapObject.User.OfferedQuests.ToArray());
        if (quests.Count == 0)
        {
            return;
        }

        await ExcelTableMgr.InitializeAsync().ConfigureAwait(false);

        for (int q = 0; q < quests.Count; q++)
        {
            ClientQuestProgress quest = quests[q];
            if (quest == null || quest.Targets == null)
            {
                continue;
            }

            for (int i = 0; i < quest.Targets.Count; i++)
            {
                QuestTaskTarget target = quest.Targets[i];
                if (target == null || target.MapIndex <= 0)
                {
                    continue;
                }

                MapTable map;
                if (!ExcelTableMgr.Maps.TryGetValue((int)target.MapIndex, out map))
                {
                    continue;
                }

                int img = map.img > 0 ? map.img : (int)target.MapIndex;
                int tileCols = (int)Math.Ceiling(map.width / (double)MapControl.TileTexWidth);
                int tileRows = (int)Math.Ceiling(map.height / (double)MapControl.TileTexHeight);
                if (tileCols <= 0 || tileRows <= 0)
                {
                    continue;
                }

                // 视野半径与 MapControl.DrawFloor 一致，保证传送落点周围一圈都已就绪。
                int rangeX = MapControl.ViewRangeX > 0 ? MapControl.ViewRangeX : 20;
                int rangeY = MapControl.ViewRangeY > 0 ? MapControl.ViewRangeY : 15;

                int colMin = (int)Math.Floor((target.X - rangeX) * MapControl.CellWidth / (double)MapControl.TileTexWidth) - 1;
                int colMax = (int)Math.Floor((target.X + rangeX) * MapControl.CellWidth / (double)MapControl.TileTexWidth) + 1;
                int rowMin = (int)Math.Floor((target.Y - rangeY) * MapControl.CellHeight / (double)MapControl.TileTexHeight) - 1;
                int rowMax = (int)Math.Floor((target.Y + rangeY + 5) * MapControl.CellHeight / (double)MapControl.TileTexHeight) + 1;

                if (colMin < 0) { colMin = 0; }
                if (rowMin < 0) { rowMin = 0; }
                if (colMax >= tileCols) { colMax = tileCols - 1; }
                if (rowMax >= tileRows) { rowMax = tileRows - 1; }

                for (int r = rowMin; r <= rowMax; r++)
                {
                    for (int c = colMin; c <= colMax; c++)
                    {
                        int index = r * tileCols + c + 1;
                        string key = img + ":" + index;

                        bool needLoad;
                        lock (_lock)
                        {
                            needLoad = _prefetched.Add(key);
                        }
                        if (!needLoad)
                        {
                            continue;
                        }

                        _ = LoadTileAsync(img, index, key);
                    }
                }
            }
        }
    }

    private static async Task LoadTileAsync(int img, int index, string key)
    {
        try
        {
            // 后台预载不抢当前地图瓦片（EResLoadPriority.Map）的下载带宽。
            await MapTileLoadGate.RunAsync(() =>
                Mir2OriginResource.GetTextureAsync(Mir2OriginResource.MapTilePath(img, index), EResLoadPriority.Background)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            lock (_lock)
            {
                _prefetched.Remove(key);   // 失败允许下次重试
            }
        }
    }
}
