using System;
using System.Collections.Generic;

using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.MSAATest;

/// <summary>
/// 多重采样(MSAA)对比：同一个「旋转细棒星爆 + 方块」场景，分别渲染进两张离屏 <see cref="RenderTarget2D"/> ——
/// 一张 <c>MultiSampleCount = 0</c>（关），一张 <c>MultiSampleCount = 4</c>（开），再把结果并排贴到屏幕。
/// 差异纯粹来自离屏目标的 MSAA，与画布上下文的 <c>antialias</c> 无关。
/// <para>
/// 这正是 WebGL 后端在「单上下文」里做 MSAA 的正确方式（也是 MonoGame 桌面端的做法：
/// 多重采样 FBO + <c>blitFramebuffer</c> 解析到可采样纹理）。浏览器里 <c>antialias</c> 是
/// <c>getContext</c> 的参数、建好上下文就改不了，因此同一个 <see cref="Game"/> 无法同时拥有
/// 两种 antialias 上下文 —— 但多重采样渲染目标可以在一个上下文内并存多个。
/// </para>
/// </summary>
public sealed class MSAATestScene : TestSceneBase
{
    private RenderTarget2D? _rtOff;
    private RenderTarget2D? _rtOn;
    private int _size;

    private Rectangle _panelOff;
    private Rectangle _panelOn;

    private readonly List<IDisposable> _owned = [];

    public override string Title => "多重采样 MSAA：离屏 RenderTarget2D（关 0 / 开 4）对比";

    private T Own<T>(T resource) where T : IDisposable
    {
        _owned.Add(resource);
        return resource;
    }

    public override void LoadContent()
    {
        _size = 420;
        // 同一尺寸两张离屏目标，唯一区别是 MultiSampleCount
        _rtOff = Own(new RenderTarget2D(Device, _size, _size, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents));
        _rtOn = Own(new RenderTarget2D(Device, _size, _size, false, SurfaceFormat.Color, DepthFormat.None, 4, RenderTargetUsage.DiscardContents));
    }

    public override void Update()
    {
        base.Update();
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

        // 离屏渲染必须在 base.Draw 的 Begin/End 之外做（SetRenderTarget / Begin / End 的调用顺序约束）。
        // 切走(SetRenderTarget(null))时，ApplyRenderTargets 会把多重采样结果解析到纹理。
        float spin = KTime.unscaledTime * 0.4f;
        RenderInto(_rtOff, spin);
        RenderInto(_rtOn, spin);
    }

    private void RenderInto(RenderTarget2D rt, float spin)
    {
        Device.SetRenderTarget(rt);
        Device.Clear(new Color(12, 15, 26));

        Batch.Begin();
        DrawScene(Batch, _size, _size, spin);
        Batch.End();

        Device.SetRenderTarget(null);
    }

    private static void DrawScene(SpriteBatch batch, int w, int h, float spin)
    {
        float cx = w / 2f, cy = h / 2f;

        // 一组从中心射出的细棒：旋转时边缘锯齿最明显
        const int bars = 48;
        for (int i = 0; i < bars; i++)
        {
            float a = i / (float)bars * MathF.PI * 2f + spin;
            float len = MathF.Min(w, h) * 0.47f;
            Color col = Hsv((i * 360f / bars + spin * 40f) % 360f, 0.85f, 1f);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Vector2(cx, cy), null, col, a,
                       new Vector2(0.5f, 0.5f), new Vector2(len, 9f), SpriteEffects.None, 0f);
        }

        // 几个旋转方块，强化边缘锯齿观感
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * MathF.PI + spin * 0.7f;
            Color col = Hsv((i * 70f) % 360f, 0.6f, 1f);
            float s = 70f + i * 26f;
            batch.Draw(KDefaultRes.DefaultTexture2D, new Vector2(cx, cy), null, col, a,
                       new Vector2(0.5f, 0.5f), new Vector2(s, s), SpriteEffects.None, 0f);
        }
    }

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        int panelW = _size, panelH = _size;
        int gap = 44;
        int totalW = panelW * 2 + gap;
        int startX = Math.Max(20, (Device.Viewport.Width - totalW) / 2);
        int y = 96;
        _panelOff = new Rectangle(startX, y, panelW, panelH);
        _panelOn = new Rectangle(startX + panelW + gap, y, panelW, panelH);

        // 全屏底
        DrawRect(batch, new Rectangle(0, 0, Device.Viewport.Width, Device.Viewport.Height), new Color(8, 10, 18));

        if (_rtOff != null) batch.Draw(_rtOff, _panelOff, Color.White);
        if (_rtOn != null) batch.Draw(_rtOn, _panelOn, Color.White);

        // 面板边框
        Border(batch, _panelOff, new Color(248, 113, 113));
        Border(batch, _panelOn, new Color(74, 222, 128));

        // 标签
        batch.DrawString(Font, "MSAA 关（MultiSampleCount = 0）", new Vector2(_panelOff.X, _panelOff.Y - 26), new Color(248, 113, 113));
        batch.DrawString(Font, "MSAA 开（MultiSampleCount = 4）", new Vector2(_panelOn.X, _panelOn.Y - 26), new Color(74, 222, 128));

        // 信息行
        float yy = _panelOff.Bottom + 18f;
        DrawLine(batch, Font,
            $"同一场景渲染进两张离屏 RT：左(采样0)边缘有锯齿 / 右(采样4)边缘平滑。" +
            $"FPS {Fps:F0}（出帧率·vsync） CPU {KTime.cpuFrameMs:F2}ms",
            new Vector2(origin.X, yy), new Color(148, 163, 184));
    }

    private static void Border(SpriteBatch batch, Rectangle r, Color c)
    {
        const int t = 2;
        DrawRectRaw(batch, new Rectangle(r.X, r.Y, r.Width, t), c);
        DrawRectRaw(batch, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        DrawRectRaw(batch, new Rectangle(r.X, r.Y, t, r.Height), c);
        DrawRectRaw(batch, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    private static void DrawRectRaw(SpriteBatch batch, Rectangle rect, Color color)
        => batch.Draw(KDefaultRes.DefaultTexture2D, rect, color);

    private static Color Hsv(float h, float s, float v)
    {
        h = ((h % 360f) + 360f) % 360f;
        float c = v * s;
        float x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
        float m = v - c;
        float r = 0, g = 0, b = 0;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        return new Color((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
