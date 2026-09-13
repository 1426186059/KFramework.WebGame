using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// 浏览器端资源 / 主机服务封装。对应 tsengine/src/core/resource.ts（mir.getBytes + mir.log）。
/// getBytes 按需拉取 URL 字节（带缓存），log 转发到 console。
/// </summary>
public static partial class BrowserResource
{
    [JSImport("mir.getBytes", "main.js")]
    private static partial byte[] GetBytesImpl(string url);

    [JSImport("mir.log", "main.js")]
    private static partial void LogImpl(string message);

    private static readonly HashSet<string> _logged404 = new HashSet<string>();

    /// <summary>
    /// 资源基址（远程 HTTP 服务器）。留空表示按站内相对路径取（同源 wwwroot）。
    /// 设置为如 "http://127.0.0.1:5080/" 后，所有资源请求都会拼到该基址上，
    /// 这样就无需把 GB 级资源复制进 wwwroot（显著节省磁盘）。
    /// 由各宿主工程在 Init 时按自身资源目录设置。
    /// </summary>
    public static string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 把游戏内的资源路径规范化为可请求的 URL：
    /// 反斜杠转正斜杠、去掉 "./" 前缀、空格转义，并在设置了 BaseUrl 时拼上基址。
    /// </summary>
    public static string ResolveUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;

        string u = url.Trim().Replace('\\', '/');
        while (u.StartsWith("./")) u = u.Substring(2);
        u = u.Replace(" ", "%20");

        if (string.IsNullOrEmpty(BaseUrl)) return u;
        return BaseUrl.TrimEnd('/') + "/" + u.TrimStart('/');
    }

    public static byte[] GetBytes(string url)
    {
        string resolved = ResolveUrl(url);
        Log($"[Mir][res] {resolved}");
        byte[] data = GetBytesImpl(resolved);
        if ((data == null || data.Length == 0) && _logged404.Add(resolved))
            Log($"[Mir] 资源加载失败(404?): {resolved}");
        return data;
    }

    // ---- 异步取字节（真正的 HTTP 异步）----
    //
    // WASM 上的 HttpClient 底层走 fetch，await 时会把控制权交还 JS 事件循环，
    // 因此不会冻结主线程（同步 XHR 会冻住渲染/输入/心跳，是被废弃的做法）。
    // 这也是跨域资源服务的推荐取法。

    private static HttpClient _http;

    // 地图片库可能非常大（如 Tiles.Lib 有数百 MB），默认的 100 秒超时会把大文件加载打断，
    // 表现为"请求发出了但始终没有 ok"。这里放宽到 30 分钟。
    private static HttpClient Http => _http ??= new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    /// <summary>异步拉取资源字节。失败（404/网络错误）返回空数组，不抛异常。</summary>
    public static Task<byte[]> GetBytesAsync(string url) => GetBytesAsyncCore(ResolveUrl(url));

    private static async Task<byte[]> GetBytesAsyncCore(string url)
    {
        if (string.IsNullOrEmpty(url)) return Array.Empty<byte>();

        Log($"[Mir][res] {url}");

        try
        {
            byte[] data = await Http.GetByteArrayAsync(url);
            if (data == null || data.Length == 0)
            {
                if (_logged404.Add(url)) Log($"[Mir] 资源为空(404?): {url}");
                return Array.Empty<byte>();
            }
            return data;
        }
        catch (Exception ex)
        {
            if (_logged404.Add(url)) Log($"[Mir] 资源加载失败: {url} : {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    public static void Log(string message)
    {
        // 直接走 Console：.NET WASM 下会进入浏览器 DevTools 控制台，不依赖 mir.log 的 JS 绑定。
        try { Console.WriteLine(message); } catch { }
        try { LogImpl(message); } catch { }
    }
}
