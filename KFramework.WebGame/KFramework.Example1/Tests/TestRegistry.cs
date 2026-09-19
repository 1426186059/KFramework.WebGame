using KFramework.MonoGameExtend;

namespace MirGame.Tests;

/// <summary>测试模块注册表：新增一个测试模块时在这里登记一项即可。</summary>
public sealed class TestEntry
{
    public required string Name { get; init; }

    public required string Desc { get; init; }

    public required Func<KSceneBase> Factory { get; init; }
}

public static class TestRegistry
{
    public static IReadOnlyList<TestEntry> Entries { get; } =
    [
        new TestEntry
        {
            Name = "字体测试",
            Desc = "系统字体 SpriteFont（Canvas2D 光栅化）与包内矢量字体 KFont（引擎自解析 ttf）：Arial / Consolas / Georgia / 黑体",
            Factory = static () => new FontTest.FontTestScene(),
        },
        new TestEntry
        {
            Name = "WebSocket 测试",
            Desc = "只做客户端：浏览器原生 WebSocket 连接自写的本地测试服务器（Tools/WebSocketTestServer，默认 ws://127.0.0.1:9000/）",
            Factory = static () => new WebSocketTest.WebSocketTestScene(),
        },
        new TestEntry
        {
            Name = "图片测试",
            Desc = "AssetBundle 整图 / 图集纹理的加载与绘制（待实现，本页只列出现有资源，供后续扩展）",
            Factory = static () => new ImageTest.ImageTestScene(),
        },
        new TestEntry
        {
            Name = "GraphicsDeviceManager",
            Desc = "设备参数管理的标准用法：点按钮切垂直同步 / 多重采样 / 全屏方式与 IsFullScreen、切分辨率，每次改动后 ApplyChanges，并展示已应用的呈现参数",
            Factory = static () => new GraphicsManagerTest.GraphicsManagerTestScene(),
        },
        new TestEntry
        {
            Name = "画布位置与尺寸",
            Desc = "C# 控制 <canvas> 的 CSS 位置与尺寸：点按钮切预设分辨率、居中 / 左上角 / 铺满视口 / 浏览器原生全屏 / 恢复页面布局（html_canvas.ts）",
            Factory = static () => new CanvasTest.CanvasTestScene(),
        },
        new TestEntry
        {
            Name = "离屏渲染 RenderTarget",
            Desc = "开 / 关 离屏渲染对比：几千个精灵，直接画 vs 画进 RenderTarget2D 后每帧只贴 1 个（看 FPS 与提交量）",
            Factory = static () => new RenderTargetTest.RenderTargetTestScene(),
        },
    ];
}
