using System;
using System.Collections.Generic;
using MirEngine;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;

using Client.Controls;
using Client.Envir;
using Client.Scenes;
using Client.Scenes.Views;
using Coroutines;
using Library;
using MirDB;
using Shared.Envir;
using Shared.Rendering;

namespace Client;

/// <summary>
/// WASM 版 CMain：等价于 Unity 移植版的 Mir3UnityAdapter/Unity/CMain.cs，
/// 把真实 Zircon 客户端的启动 / 主循环 / 输入桥接进浏览器。
/// 与 Unity 的 MonoBehaviour 生命周期不同，这里由 main.js 的 requestAnimationFrame
/// 每帧调 Frame()，并把鼠标/键盘事件路由到 DXControl.ActiveScene。
/// </summary>
public static partial class MirClientHost
{
    private static bool _ready;

    private static void MirLog(string message) => MirEngine.BrowserResource.Log(message);

    // ===================== 生命周期 =====================

    /// <summary>
    /// 预下载清单：直接复用 Libraries.GetUrl 把 Libraries.LibraryList 全量规范化为
    /// Web URL（MyRes/Data/MapData/Tilesc.Zl 等，已处理 "Map Data"→"MapData" 空格与 Data/ 前缀）。
    /// 缺失的文件由 main.js 容错跳过，因此把真实 Data\ 全套 .Zl 丢进 wwwroot/MyRes/Data/ 即可自动全加载。
    /// </summary>
    [JSExport]
    public static string[] GetAssetList()
    {
        var urls = new List<string>();
        foreach (LibraryFile file in Libraries.LibraryList.Keys)
        {
            string url = Libraries.GetUrl(file);
            if (!string.IsNullOrEmpty(url))
                urls.Add(url);
        }
        return urls.ToArray();
    }

    /// <summary>
    /// 启动期预加载清单：仅包含连接前必须、且同步取会冻结主线程导致心跳超时的资源——
    /// 数据库配置表 System.db / Users.db。main.js 在 game.Init() 后用异步 fetch 预热进 host.assets 缓存，
    /// 之后 LoadDatabase 经同步 getBytes 命中缓存即瞬时返回，不再阻塞主线程。
    /// 库/地图等仍走按需懒加载（避免 GB 级预下载）。
    /// </summary>
    [JSExport]
    public static string[] GetPreloadUrls()
    {
        return new[]
        {
            "MyRes/Data/System.db",
            "MyRes/Data/Users.db",
        };
    }

    [JSExport]
    public static void Init()
    {
        try
        {
            ConfigReader.Load(Assembly.GetAssembly(typeof(Config)));
            CEnvir.LoadLanguage();

            MirLibrary.GetNow = () => CEnvir.Now;
            MirLibrary.GetCacheDuration = () => Config.CacheDuration;
            MirLibrary.GetUseZlAtlasPages = () => Config.UseZlAtlasPages;
            MirLibrary.DrawCounted = () => CEnvir.DPSCounter++;
            // 资源基址：指向 Zircon 客户端资源目录对应的本地 HTTP 服务
            // （D:\OpenSource\Zircon\Debug\Client），避免把 GB 级资源复制进 wwwroot。
            MirEngine.BrowserResource.BaseUrl = "http://127.0.0.1:5081/";
            MirLibrary.GetBytesFromUrl = MirEngine.BrowserResource.GetBytes;
            // 数据库配置表（System.db / Users.db）与库/地图同走 HTTP 取字节；Session 内部按文件名映射到 MyRes/Data/。
            // 用 GetFileName 仅取文件名，避免把 ".\Data\System.db" 这类相对路径拼进 URL（会 404）。
            Session.DatabaseBytesLoader = path =>
            {
                // WASM 运行时 Path 按 Unix 语义：反斜杠 \ 不是分隔符，GetFileName 不会剥离 ".\Data\" 前缀。
                // 故手动按 / 和 \ 两种分隔符取末段文件名。
                var name = path;
                var idx = name.LastIndexOfAny(new char[] { '/', '\\' });
                if (idx >= 0) name = name.Substring(idx + 1);
                if (string.IsNullOrEmpty(name)) name = "System.db";
                var url = "MyRes/Data/" + name;
                PrintTool.Write("DB-LOADER", $"path='{path}' -> url='{url}'");
                return MirEngine.BrowserResource.GetBytes(url);
            };
            CEnvir.SaveChatLogLine = text => MirEngine.BrowserStorage.AppendText("mir_chat_log", text);

            LoadLibraries();

            CEnvir.Init(new string[0]);

            CEnvir.Target = new TargetForm();
            CEnvir.Target.ClientSize = new Size(1024, 768);
            Config.GameSize = new Size(1024, 768);
            Config.LimitFPS = false; // 浏览器主线程不可 Sleep
            Config.MapPath = "MyRes/Map/";
            MapControl.MapBytesLoader = f => MirEngine.BrowserResource.GetBytes(Config.MapPath + f);

            string requested = RenderingPipelineManager.NormalizePipelineId("Canvas");
            if (!string.Equals(Config.RenderingPipeline, requested, StringComparison.OrdinalIgnoreCase))
                Config.RenderingPipeline = requested;

            string active = RenderingPipelineManager.InitializeWithFallback(
                requested,
                new RenderingPipelineContext(CEnvir.Target, CreateRenderingHostSettings()));
            if (!string.Equals(Config.RenderingPipeline, active, StringComparison.OrdinalIgnoreCase))
                Config.RenderingPipeline = active;

            try
            {
                DXSoundManager.Create();
            }
            catch (Exception ex)
            {
                MirLog($"[Mir] 声音初始化跳过: {ex.Message}");
            }

            DXControl.ActiveScene = new LoginScene(Config.ExtendedLogin ? Config.GameSize : Config.IntroSceneSize);

            _ready = true;
            ConfigureInput();
            MirLog("[Mir] 真实 Zircon 客户端初始化完成（CMain 等价驱动器已就绪）。");
        }
        catch (Exception ex)
        {
            MirLog($"[Mir] 初始化异常: {ex}");
        }
    }

    /// <summary>
    /// 仅登记懒加载入口（MirLibrary(url, fromUrl:true)），字节在首次绘制对应库时由
    /// MirLibrary.GetBytesFromUrl（= mir.getBytes）按需拉取，避免启动即下载 GB 级资源。
    /// 缺失或无法登记的库自动跳过，保证初始化不因个别资源缺失而中断。
    /// </summary>
    private static void LoadLibraries()
    {
        foreach (KeyValuePair<LibraryFile, string> pair in Libraries.LibraryList)
        {
            string url = Libraries.GetUrl(pair.Key);
            if (string.IsNullOrEmpty(url))
                continue;

            try
            {
                CEnvir.LibraryList[pair.Key] = new MirLibrary(url, true);
            }
            catch (Exception ex)
            {
                MirLog($"[Mir] 库登记失败 {pair.Key}: {ex.Message}");
            }
        }

        MirLog($"[Mir] 已登记库 {CEnvir.LibraryList.Count} / {Libraries.LibraryList.Count}");
    }

    [JSExport]
    public static void Frame(double timeMs)
    {
        if (!_ready) return;
        CoroutineManager.Instance.Update(); // 每帧先推进协程（Unity 风格：每帧每协程只前进一个 yield 边界，分帧加载 DB 等），保证心跳不被冻结
        CEnvir.GameLoop();
    }

    // ===================== 输入 =====================
    // 浏览器键鼠输入统一经 JSBind/BrowserKeyboard + BrowserMouse 封装：
    // DOM 事件 -> WinForms 风格 EventArgs -> C# 事件，此处仅做订阅与路由
    // （复刻原 On* 的行为：设置 MouseLocation 并转发到 ActiveScene）。

    private static void ConfigureInput()
    {
        MirEngine.BrowserKeyboard.Attach();
        MirEngine.BrowserMouse.Attach();

        MirEngine.BrowserMouse.MouseDown += (s, e) =>
        {
            CEnvir.MouseLocation = new Point(e.X, e.Y);
            DXControl.ActiveScene?.OnMouseDown(e);
        };
        MirEngine.BrowserMouse.MouseMove += (s, e) =>
        {
            CEnvir.MouseLocation = new Point(e.X, e.Y);
            DXControl.ActiveScene?.OnMouseMove(e);
        };
        MirEngine.BrowserMouse.MouseUp += (s, e) =>
        {
            DXControl.ActiveScene?.OnMouseUp(e);
            DXControl.ActiveScene?.OnMouseClick(e);
        };
        MirEngine.BrowserMouse.MouseWheel += (s, e) =>
        {
            CEnvir.MouseLocation = new Point(e.X, e.Y);
            DXControl.ActiveScene?.OnMouseWheel(e);
        };
        MirEngine.BrowserKeyboard.KeyDown += (s, e) => DXControl.ActiveScene?.OnKeyDown(e);
        MirEngine.BrowserKeyboard.KeyUp += (s, e) => DXControl.ActiveScene?.OnKeyUp(e);
        MirEngine.BrowserKeyboard.KeyPress += (s, e) => DXControl.ActiveScene?.OnKeyPress(e);
    }

    // ===================== 渲染宿主设置（复刻 Client/Program.cs 的 CreateRenderingHostSettings）=====================

    private static RenderingHostSettings CreateRenderingHostSettings()
    {
        return new RenderingHostSettings
        {
            Now = () => CEnvir.Now,
            SaveException = CEnvir.SaveException,
            InvalidateRenderCaches = InvalidateRenderCaches,
            FullScreenChanged = fullScreen =>
            {
                if (DXConfigWindow.ActiveConfig?.FullScreenCheckBox != null)
                    DXConfigWindow.ActiveConfig.FullScreenCheckBox.Checked = fullScreen;
            },
            GetActiveSceneSize = () => DXControl.ActiveScene?.Size ?? Config.GameSize,
            GetDefaultMonitor = () => Config.DefaultMonitor,
            SetDefaultMonitor = v => Config.DefaultMonitor = v,
            GetRenderingPipeline = () => Config.RenderingPipeline,
            SetRenderingPipeline = v => Config.RenderingPipeline = v,
            GetGameSize = () => Config.GameSize,
            SetGameSize = v => Config.GameSize = v,
            GetFullScreen = () => Config.FullScreen,
            SetFullScreen = v => Config.FullScreen = v,
            GetBorderless = () => Config.Borderless,
            SetBorderless = v => Config.Borderless = v,
            GetVSync = () => Config.VSync,
            SetVSync = v => Config.VSync = v,
            GetUseD3D11SpriteBatch = () => Config.UseD3D11SpriteBatch,
            SetUseD3D11SpriteBatch = v => Config.UseD3D11SpriteBatch = v,
        };
    }

    private static void InvalidateRenderCaches()
    {
        var visited = new HashSet<DXControl>();
        InvalidateControlTree(DXControl.ActiveScene, visited);

        foreach (DXControl messageBox in DXControl.MessageBoxList.ToArray())
            InvalidateControlTree(messageBox, visited);

        InvalidateControlTree(DXControl.MouseControl, visited);
        InvalidateControlTree(DXControl.FocusControl, visited);

        foreach (MirLibrary library in CEnvir.LibraryList.Values)
            library?.DisposeTextures();
    }

    private static void InvalidateControlTree(DXControl control, HashSet<DXControl> visited)
    {
        if (control == null || !visited.Add(control))
            return;

        control.DisposeTexture();

        foreach (DXControl child in control.Controls.ToArray())
            InvalidateControlTree(child, visited);
    }
}
