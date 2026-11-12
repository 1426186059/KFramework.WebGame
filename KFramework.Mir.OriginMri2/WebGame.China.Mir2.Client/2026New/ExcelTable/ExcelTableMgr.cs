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

    /// <summary>
    /// 1) 预加载所需 config 分片（0config=map，2config=modelInfo 等），整文件反序列化为 DTO；
    /// 2) 再从 DTO 里取出特定表（map、modelInfo）。分片只下载一次。
    /// 全程走源生成 + JsonTool，AOT 安全。失败仅记录日志，不抛出——配置表不是进游戏的硬前置。
    /// </summary>
    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsReady) return;
        try
        {
            PrintTool.Log("[ExcelTableMgr] Start initializing");
            // 1) 预加载 config 分片
            await PreloadShardsAsync(new[] { 0, 2 }, ct).ConfigureAwait(false);

            PrintTool.Log("[ExcelTableMgr] 配置表预加载完成");

            PrintTool.Log("[ExcelTableMgr] IsReady True");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ExcelTableMgr] 配置表加载失败：{ex}");
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
