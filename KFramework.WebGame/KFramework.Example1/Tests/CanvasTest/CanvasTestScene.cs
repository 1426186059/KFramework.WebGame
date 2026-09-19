using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.CanvasTest;

/// <summary>
/// 画布（<c>&lt;canvas&gt;</c>）位置与尺寸控制：<see cref="Html5Canvas"/> ↔ <c>html_canvas.ts</c>。
/// <para>· <see cref="GameWindow.SetCanvasRect"/>：把画布摆到页面某个位置并指定 CSS 尺寸；</para>
/// <para>· <see cref="GameWindow.SetCanvasFullscreen"/>：还原成铺满视口（启动时的默认状态，页面里没有 canvas 时引擎也是这样自动建一块的）。</para>
/// </summary>
/// <remarks>
/// <para>操作：1 小窗 640×360（左上角）、2 居中 800×480、3 还原全屏。</para>
/// <para>C# 只改 CSS，下面的 CSS 尺寸 / 后备缓冲 / 视口三行会在下一帧自动跟上
///（每帧 <see cref="GraphicsDevice.SyncCanvasSize"/> 同步，触发 <see cref="GameWindow.SizeChanged"/>）。</para>
/// </remarks>
public sealed class CanvasTestScene : TestSceneBase
{
    public override string Title => "画布位置与尺寸：Html5Canvas（1 小窗 / 2 居中 / 3 全屏）";

    private GameWindow Window => KSceneMgr.Game.Window;

    public override void Update()
    {
        base.Update();
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

        if (Input_KeyBoard.GetKeyDown(Keys.D1)) Window.SetCanvasRect(40, 40, 640, 360);
        if (Input_KeyBoard.GetKeyDown(Keys.D2))
        {
            // 居中：CSS 尺寸减去画布尺寸再折半（CSS 尺寸 ≈ 后备缓冲 / DPR）
            int x = (int)((Device.CssSize.X - 800) / 2);
            int y = (int)((Device.CssSize.Y - 480) / 2);
            Window.SetCanvasRect(Math.Max(0, x), Math.Max(0, y), 800, 480);
        }
        if (Input_KeyBoard.GetKeyDown(Keys.D3)) Window.SetCanvasFullscreen();
    }

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        float x = origin.X;
        float y = origin.Y;

        GameWindow window = Window;

        y += DrawSection(batch, "① 当前画布", new Vector2(x, y));
        y += DrawLine(batch, Font, $"DOM id：{window.CanvasId}", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"CSS 尺寸：{(int)Device.CssSize.X} x {(int)Device.CssSize.Y}（布局尺寸）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"后备缓冲：{Device.Viewport.Width} x {Device.Viewport.Height}（= CSS × DPR {Device.DevicePixelRatio:0.##}）",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font, $"同步一帧后三者自动一致；SizeChanged 由 Game.TickFrame 触发。", new Vector2(x, y), new Color(150, 165, 190));

        y += 16f;
        y += DrawSection(batch, "② 可用 API", new Vector2(x, y));
        y += DrawLine(batch, Font, "GameWindow.SetCanvasRect(x, y, width, height)", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "GameWindow.SetCanvasFullscreen()", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "Html5Canvas.Create / CreateFullscreen / Destroy / Exists（多画布场景）", new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "③ 注意", new Vector2(x, y));
        y += DrawLine(batch, Font, "· 页面里已有同 id 画布就用它，没有才由引擎自动创建全屏画布", new Vector2(x, y), new Color(255, 190, 140));
        y += DrawLine(batch, Font, "· 改尺寸会重建 backing buffer，别每帧调用", new Vector2(x, y), new Color(255, 190, 140));
    }
}
