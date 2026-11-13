using KFramework.MonoGame;

public static partial class ExcelTableMgr
{
    // ── 预加载的 config 分片 ──
    // 0config / 2config：直接整文件反序列化为 DTO，避免逐条二次解析
    public static OriginMir2Config0 _config0;
    public static OriginMir2Config2 _config2;

    // ── 已加载的表（直接从 config DTO 派生，无需在 InitializeAsync 里单独赋值）──
    // Maps 与 0config 的 map 表完全一致，直接返回。
    public static Dictionary<int, MapTable> Maps => _config0?.map ?? new Dictionary<int, MapTable>();
    // 模型枢轴/帧间隔/方向数/动作清单等查询见 partial 类 ExcelTableMgr.Func.cs（按 model+action 二维查询，
    // 不再用 (long)model*1000+action 一维 long 键 hack）。
    public static bool IsReady => _config0 != null && _config2 != null;

    // ── 物品表（独立于 config 分片，从本地 MyRes/ExcelConfig/ItemInfo.json 加载）──
    // GM 面板等需要全量物品的场合直接取 ExcelTableMgr.Items，不再各自异步拉表。
    public static List<ItemTableDTO> _items;
    public static Dictionary<int, ItemTableDTO> _itemsByIndex;   // Index → DTO 索引（按物品 Index 快速查原始 uint 图标号等）
    public static List<ItemTableDTO> Items => _items ?? new List<ItemTableDTO>();
    public static bool ItemsReady => _items != null;

    /// <summary>按物品 Index 查 ItemTableDTO。用于客户端补全被网络协议截断的字段
    ///（ItemInfo.Image 协议传输是 ushort，换表后 46% 图标号 >65535 被截断；本地表存原始 uint）。</summary>
    public static bool TryGetItem(int index, out ItemTableDTO item)
    {
        item = null;
        return _itemsByIndex != null && _itemsByIndex.TryGetValue(index, out item) && item != null;
    }


    /// <summary>
    /// 1) 预加载所需 config 分片（0config=map，2config=modelInfo 等），整文件反序列化为 DTO；
    /// 2) 再从 DTO 里取出特定表（map、modelInfo）。分片只下载一次。
    /// 全程走源生成 + JsonTool，AOT 安全。失败仅记录日志，不抛出——配置表不是进游戏的硬前置。
    /// </summary>
    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            if (!IsReady)
            {
                PrintTool.Log("[ExcelTableMgr] Start initializing");
                // 1) 预加载 config 分片
                await PreloadShardsAsync(new[] { 0, 2 }, ct).ConfigureAwait(false);

                PrintTool.Log("[ExcelTableMgr] 配置表预加载完成");

                PrintTool.Log("[ExcelTableMgr] IsReady True");
            }
            // 2) 物品表独立加载（不并入 IsReady：config 已就绪后再次调用仍会补拉物品表）
            await LoadItemsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ExcelTableMgr] 配置表加载失败：{ex}");
        }
    }

    /// <summary>从本地资源服务器加载 MyRes/ExcelConfig/ItemInfo.json（12594 项物品）。
    /// 走源生成 JsonTool，AOT 安全。失败仅记录日志，不抛出。</summary>
    private static async Task LoadItemsAsync(CancellationToken ct)
    {
        if (_items != null) return;
        try
        {
            PrintTool.Log("[ExcelTableMgr] 开始加载 ItemInfo.json");
            byte[] bytes = await BrowserResource.GetBytesAsync("MyRes/ExcelConfig/ItemInfo.json", ct).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
            {
                PrintTool.Log("[ExcelTableMgr] ItemInfo.json 为空（检查 127.0.0.1:5080/MyRes/ExcelConfig/）");
                return;
            }
            _items = JsonTool.FromJson<List<ItemTableDTO>>(
                System.Text.Encoding.UTF8.GetString(bytes),
                AppJsonContext.Default.ItemList);
            // 建 Index → DTO 索引：客户端用它补全协议截断的字段（ItemInfo.Image 协议是 ushort，本地表存原始 uint）
            var byIdx = new Dictionary<int, ItemTableDTO>(_items?.Count ?? 0);
            if (_items != null)
                foreach (var it in _items)
                    if (it != null && it.Index > 0 && !byIdx.ContainsKey(it.Index))
                        byIdx[it.Index] = it;
            _itemsByIndex = byIdx;
            PrintTool.Log($"[ExcelTableMgr] ItemInfo 加载完成 {(_items?.Count ?? 0)} 项，索引 {byIdx.Count} 条");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ExcelTableMgr] ItemInfo 加载失败：{ex}");
        }
    }

    private static async Task PreloadShardsAsync(int[] shards, CancellationToken ct)
    {
        foreach (var shard in shards)
        {
            if (shard == 0)
            {
                if (_config0 != null) continue;
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 0000");
                byte[] bytes = await Mir2OriginResource.GetConfigShardBytesAsync(shard, ct).ConfigureAwait(false);
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 11111");
                if (bytes == null || bytes.Length == 0) continue;
                var info = AppJsonContext.Default.OriginMir2Config0;
                _config0 = JsonTool.FromJson<OriginMir2Config0>(System.Text.Encoding.UTF8.GetString(bytes), info);
            }
            else if (shard == 2)
            {
                if (_config2 != null) continue;
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 0000");
                byte[] bytes = await Mir2OriginResource.GetConfigShardBytesAsync(shard, ct).ConfigureAwait(false);
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 11111");
                if (bytes == null || bytes.Length == 0) continue;
                var info = AppJsonContext.Default.OriginMir2Config2;
                _config2 = JsonTool.FromJson<OriginMir2Config2>(System.Text.Encoding.UTF8.GetString(bytes), info);
            }
            else
            {
                
            }
        }
    }
}
