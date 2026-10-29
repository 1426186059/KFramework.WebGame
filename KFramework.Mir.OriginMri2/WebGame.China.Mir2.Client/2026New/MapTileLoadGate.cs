using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 地图瓦片加载并发闸门。
/// <para>
/// 进图瞬间 GameScene.GetMapTile 会在单帧内触发 50+ 个瓦片请求，若不限制会：
///   1) 挤爆同域连接池（浏览器 / CDN 约 6 并发上限），队尾瓦片排队到 20s；
///   2) 多个瓦片同时下完后的「解码 + 上传 GPU」集中爆发，造成主线程尖峰。
/// </para>
/// <para>
/// 注意：ContentLoadScheduler（KFramework.MonoGame）的 6 并发只罩住 ContentFunc 里
/// 「缓存未命中 → EnqueueAsync 下载」那一段；瓦片走 mCacheInstance.LoadAsync 读取命中 /
/// 读穿透后直接 return，并未进入该闸门，故瓦片加载需要在此单独限流。
/// </para>
/// </summary>
public static class MapTileLoadGate
{
    // 与 ContentLoadScheduler 默认并发一致，避免同域连接被挤爆。
    private static readonly SemaphoreSlim _gate = new SemaphoreSlim(6, 6);

    /// <summary>
    /// 在并发闸门内执行一次瓦片加载（下载 + 解码 + GPU 上传），同时最多 6 个在跑。
    /// 异常会原样向外抛出，调用方自行处理；无论成功失败都会释放并发槽。
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> loader, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await loader().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
