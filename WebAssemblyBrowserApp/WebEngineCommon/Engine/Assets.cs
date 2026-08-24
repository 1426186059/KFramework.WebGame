#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 已加载的图片资源句柄（三端统一）。
/// Id 为各端渲染器的纹理/图片标识；Ready 表示可安全绘制。
/// </summary>
public sealed class Texture
{
    public string Url { get; init; } = "";

    //Id 本质上是 "GPU 纹理槽位索引"，不是 Web 的概念。-1 表示"已下载但未上传"或"加载失败"的中间状态。
    public int Id { get; init; } = -1; 
    public int Width { get; init; }
    public int Height { get; init; }
    public bool Ready => Id >= 0;
}

/// <summary>
/// 统一资源加载模块：三端共用同一套图片「加载 / 缓存 / 绘制」API。
/// 底层由各端 main.js 注册的 assets 桥实现（Canvas2D / WebGL / WebGPU 各自适配），
/// 游戏代码无需关心后端差异，也不用管加载时序（未就绪自动跳过绘制）。
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class Assets
{
    private static readonly Dictionary<string, Texture> _cache = new();
    private static readonly Dictionary<string, Task<Texture>> _pending = new();

    [JSImport("assets.loadImage", "main.js")]
    private static partial Task<JSObject?> LoadImageAsync(string url);

    [JSImport("assets.loadVideo", "main.js")]
    private static partial Task<JSObject?> LoadVideoJsAsync(string url);

    [JSImport("assets.drawImage", "main.js")]
    private static partial void JsDrawImage(int id, float x, float y, float w, float h);

    [JSImport("assets.uploadTexture", "main.js")]
    private static partial void JsUploadTexture(int id, int w, int h, int[] argb);

    [JSImport("assets.disposeTexture", "main.js")]
    private static partial void JsDisposeTexture(int id);

    /// <summary>
    /// 异步加载图片（带缓存去重；并发请求共享同一次加载）。
    /// 加载失败也返回 Texture（Ready=false），不会抛异常。
    /// </summary>
    public static Task<Texture> LoadAsync(string url)
    {
        if (_cache.TryGetValue(url, out var cached)) return Task.FromResult(cached);
        if (_pending.TryGetValue(url, out var running)) return running;

        var task = LoadCoreAsync(url);
        _pending[url] = task;
        return task;
    }

    private static async Task<Texture> LoadCoreAsync(string url)
    {
        int id = -1, w = 0, h = 0;
        using var obj = await LoadImageAsync(url);
        if (obj is not null && obj.GetPropertyAsInt32("id") >= 0)
        {
            id = obj.GetPropertyAsInt32("id");
            w = (int)obj.GetPropertyAsDouble("w");
            h = (int)obj.GetPropertyAsDouble("h");
        }
        var tex = new Texture { Url = url, Id = id, Width = w, Height = h };
        _pending.Remove(url);
        _cache[url] = tex;
        return tex;
    }

    // ------------------------- 视频纹理（GPU 硬解） -------------------------

    private static readonly Dictionary<string, Texture> _videoCache = new();
    private static readonly Dictionary<string, Task<Texture>> _videoPending = new();

    /// <summary>
    /// 异步加载视频为纹理（带缓存去重）。解码由浏览器硬件解码器完成，绘制时当前帧
    /// 直接进 GPU（WebGPU 走 <c>importExternalTexture</c> 零拷贝导入；WebGL/Canvas2D 直接
    /// 以 video 为源绘制），全程无 CPU 像素解码/拷贝 —— 即真正的「GPU 解码」路径。
    /// 返回的 Texture 可照常传给 <see cref="Draw(Texture?,float,float,float,float)"/>。
    /// </summary>
    public static Task<Texture> LoadVideoAsync(string url)
    {
        if (_videoCache.TryGetValue(url, out var cached)) return Task.FromResult(cached);
        if (_videoPending.TryGetValue(url, out var running)) return running;

        var task = LoadVideoCoreAsync(url);
        _videoPending[url] = task;
        return task;
    }

    private static async Task<Texture> LoadVideoCoreAsync(string url)
    {
        int id = -1, w = 0, h = 0;
        using var obj = await LoadVideoJsAsync(url);
        if (obj is not null && obj.GetPropertyAsInt32("id") >= 0)
        {
            id = obj.GetPropertyAsInt32("id");
            w = (int)obj.GetPropertyAsDouble("w");
            h = (int)obj.GetPropertyAsDouble("h");
        }
        var tex = new Texture { Url = url, Id = id, Width = w, Height = h };
        _videoPending.Remove(url);
        _videoCache[url] = tex;
        return tex;
    }

    /// <summary>已缓存（无论成功失败）的纹理；未加载过返回 null。</summary>
    public static Texture? Get(string url)
        => _cache.TryGetValue(url, out var tex) ? tex : null;

    /// <summary>把纹理绘制到屏幕 (x, y, w, h)。纹理未就绪时自动跳过，不会报错。</summary>
    public static void Draw(Texture? tex, float x, float y, float w, float h)
    {
        if (tex is { Ready: true }) JsDrawImage(tex.Id, x, y, w, h);
    }

    /// <summary>绘制动态纹理（<see cref="Texture2D"/>）。未 Commit 前自动跳过。</summary>
    public static void Draw(Texture2D tex, float x, float y, float w, float h)
    {
        if (tex is { Ready: true }) JsDrawImage(tex.Id, x, y, w, h);
    }

    // ------------------------- 直接上传 GPU 纹理 -------------------------

    /// <summary>
    /// 直接把像素数组上传为 GPU 纹理（等效 <see cref="Texture2D.Commit"/>，但不依赖 Texture2D 对象）。
    /// 适合已有独立像素缓冲的用途（如 FC PPU 帧缓冲）：id 由调用方自定义，JS 端 'dyn:' 命名空间
    /// 与图片 id 隔离不冲突；之后可用 <see cref="Draw(int,float,float,float,float)"/> 按 id 直接绘制。
    /// 像素格式 ARGB8888；对同 id 再次调用即更新纹理内容（尺寸变化时自动重建）。
    /// </summary>
    public static void UploadTexture(int id, int width, int height, int[] argb)
    {
        ArgumentNullException.ThrowIfNull(argb);
        if (argb.Length != width * height)
            throw new ArgumentException($"像素长度 {argb.Length} 与 {width}×{height} 不匹配。", nameof(argb));
        JsUploadTexture(id, width, height, argb);
    }

    /// <summary>释放 <see cref="UploadTexture(int,int,int,int[])"/> 上传的 GPU 纹理。</summary>
    public static void DisposeTexture(int id) => JsDisposeTexture(id);

    /// <summary>按纹理句柄 id 直接绘制（图片 id 或动态纹理 id 均可）。</summary>
    public static void Draw(int id, float x, float y, float w, float h)
        => JsDrawImage(id, x, y, w, h);
}
