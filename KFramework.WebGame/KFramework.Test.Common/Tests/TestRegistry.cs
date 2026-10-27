using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests
{

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
                Desc = "四条字体路线对比：SpriteFont（Canvas2D 光栅化，多图集）/ KFont（引擎自解析 ttf）/ BitmapFont（位图字体，本页运行时烘焙 ASCII）/ MSDF（占位未实现）；按钮切模式，空格切 1000→9000 字共 9 档",
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
                Desc = "图片解析两种路径：① 远程加载 ContentManager.LoadTexture2DAsync（jpg/png/webp）；② AssetBundle.LoadTexture 遍历已加载包纹理（Rgba/Png/Webp/Ktx2 按构建格式覆盖）。资源在 Content/raw。",
                Factory = static () => new ImageTest.ImageTestScene(),
            },
            new TestEntry
            {
                Name = "画布位置与尺寸",
                Desc = "C# 控制 <canvas> 的 CSS 位置与尺寸：点按钮切预设分辨率、居中 / 左上角 / 填满整个 HTML 页面（软全屏）/ 恢复页面布局（html_canvas.ts）",
                Factory = static () => new CanvasTest.CanvasTestScene(),
            },
            new TestEntry
            {
                Name = "离屏渲染 RenderTarget",
                Desc = "开 / 关 离屏渲染对比：几千个精灵，直接画 vs 画进 RenderTarget2D 后每帧只贴 1 个（看 FPS 与提交量）",
                Factory = static () => new RenderTargetTest.RenderTargetTestScene(),
            },
            new TestEntry
            {
                Name = "输入测试",
                Desc = "鼠标 / 键盘状态：按下、抬起、持续按住（按帧计数）；以及用鼠标左键拖拽物体（方块 A / B 跟随光标移动）",
                Factory = static () => new InputTest.InputTestScene(),
            },
            new TestEntry
            {
                Name = "触摸测试",
                Desc = "多触点状态（按下/移动/静止/抬起）、位置与位移、双指 Pinch 缩放，以及手势 Tap / LongPress / Swipe；电脑上用浏览器设备模拟（Ctrl+Shift+M）产生触摸事件",
                Factory = static () => new TouchTest.TouchTestScene(),
            },
            new TestEntry
            {
                Name = "文本渲染 / KLabel / 输入框（按钮切页）",
                Desc = "① TextRenderer 与 ② KLabel 共用同一套矩形与样本（TextAnchor 九向各占一格、HorizontalWrapMode 的 Wrap/Overflow、VerticalWrapMode 的 Truncate/Overflow），两页对照可分离「文本库问题」与「控件层换算问题」；③ TextBox：点击聚焦、键入/IME 中文、多行",
                Factory = static () => new TextRenderTest.TextRenderTestScene(),
            },
            new TestEntry
            {
                Name = "KCanvas 组件（KButton / KLabel / KImage / KSimpleLabel）",
                Desc = "节点树 UI 体系：组件挂在 KUIRoot 的 KCanvas 上（由 KSceneMgr 驱动，Anchor/Pivot 布局 + 全局指针分发）。每个组件一个页签 —— KButton 九个按钮用 Anchor=Pivot 钉在固定矩形九处（文字居中、四态过渡）；KLabel 与它同构（Anchor 决定控件落点、Alignment 只管格内）+ 折行；KImage 尺寸/染色/Pivot；KSimpleLabel 纯 Pivot 定位九处",
                Factory = static () => new KCanvasTest.KCanvasTestScene(),
            },
            new TestEntry
            {
                Name = "混合模式 Blend",
                Desc = "BlendState 各混合模式可视化对比：半透明 / 加法发光 / 覆盖 / 乘法压暗，以及传奇昼夜用的 Multiply(Zero, SourceColor)；并演示用公开构造传自定义混合参数",
                Factory = static () => new BlendTest.BlendTestScene(),
            },
            new TestEntry
            {
                Name = "相机自动合批 KCamera",
                Desc = "v1+v2 演示：两个相机各合各的批、各画各的目标（离屏 RT / 画布），场景 Draw 里零 Begin/End/SetRenderTarget；屏幕相机按 CullingMask 层剔除只画 UI 层，离屏相机只画世界层，PreDraw 把 RT 合成回屏幕",
                Factory = static () => new CameraBatchScene(),
            },
            new TestEntry
            {
                Name = "相机测试（2D正交/3D透视/视锥裁剪）",
                Desc = "专项测试：2D 正交相机世界↔屏幕坐标往返 + 正交视锥剔除（环上越界精灵被 activeSelf=false 真正剔除）；3D 透视相机（看向 -Z）用 IsInFrustum 断言 前方/背后/超远/过近/FOV外/FOV内 各用例，文本报告 PASS/FAIL。对标 Unity 相机矩阵",
                Factory = static () => new CameraTest.CameraTestScene(),
            },
            new TestEntry
            {
                Name = "PrintTool 打印性能",
                Desc = "三种打印方式对比：① Generic.Log(object 装箱) ② PrintTool.A(StringBuilder 后端) ③ PrintTool.B(Span<char> 后端)，各打印几千条并计时 + 统计 GC0 次数；可切数量档位、单测或全测。请用 Debug 构建运行（PrintTool.A/B 方法带 [Conditional]，Release 下被剔除）",
                Factory = static () => new PrintToolTest.PrintToolTestScene(),
            },
        ];
    }

}
