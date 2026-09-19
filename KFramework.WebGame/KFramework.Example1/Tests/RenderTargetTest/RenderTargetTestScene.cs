using System.Diagnostics;

using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.RenderTargetTest;

/// <summary>
/// 离屏渲染（<see cref="RenderTarget2D"/>）对比测试：同一片「几千个精灵」的螺旋画面，两种画法：
/// <para>· <b>关</b>（直接绘制）：每帧把 N 个精灵全部塞进 SpriteBatch，N 次顶点变换 + 一次大批量提交。</para>
/// <para>· <b>开</b>（离屏渲染）：画面先画进一张离屏纹理（内容不变时只画一次），之后每帧只贴 1 个精灵。</para>
///
/// <para>操作：空格 / 点按钮切离屏开关，A 切画面动画，← → 或 1..5 切精灵数量。</para>
/// <para>注意（照 MonoGame 的约束）：<c>SetRenderTarget</c> 必须在 <c>SpriteBatch.Begin</c> 之前、
/// <c>End</c> 之后调用，本场景因此把离屏绘制放在 <see cref="Draw"/> 里、交给基类 Begin 之前完成。</para>
///
/// <para>精灵纹理由代码程序化生成（带光照的球），不依赖任何图片资源与打包流程。</para>
/// </summary>
public sealed class RenderTargetTestScene : TestSceneBase
{
    /// <summary>可选精灵数量档位。</summary>
    private static readonly int[] CountOptions = [1000, 3000, 5000, 10000, 20000];

    private int _countIndex = 2;          // 默认 5000
    private bool _useRenderTarget = true; // 离屏开关
    private bool _animate;                // 画面是否动（动的话离屏缓存每帧都得重画，优势消失）

    private Texture2D? _ball;
    private RenderTarget2D? _rt;
    private bool _rtDirty = true;

    private readonly List<IDisposable> _owned = [];
    private readonly List<Button> _buttons = [];
    private Rectangle _stage;

    // ---- 统计 ----
    private readonly Stopwatch _stopwatch = new();
    private float _fps = 60f;
    private float _drawMs;              // 整帧（离屏 + 屏幕）绘制耗时 EMA
    private float _rtDrawMs;            // 其中离屏那段（不重绘时为 0）
    private float _screenDrawMs;        // 其中屏幕那段
    private long _screenSprites;
    private long _screenDrawCalls;
    private long _rtSprites;        // 上一次离屏重绘提交的精灵数（静态时一直复用，不再重画）
    private int _rtRedraws;

    public override string Title => "离屏渲染 RenderTarget2D：开 / 关 对比（几千个精灵）";

    public override void LoadContent()
    {
        // 程序化生成一张带光照的球（RGBA8），省掉图片资源与打包流程；绘制时再用 Color 着色。
        _ball = Own(MakeBallTexture(Device, 32));
    }

    #region 交互

    public override void Update()
    {
        // 基类处理 Esc / 左上角返回；一旦切走就不要再碰本场景的状态。
        base.Update();
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

        Layout();

        if (Input_KeyBoard.GetKeyDown(Keys.Space)) ToggleRenderTarget();
        if (Input_KeyBoard.GetKeyDown(Keys.A)) ToggleAnimate();
        if (Input_KeyBoard.GetKeyDown(Keys.Left) || Input_KeyBoard.GetKeyDown(Keys.Down)) ChangeCount(-1);
        if (Input_KeyBoard.GetKeyDown(Keys.Right) || Input_KeyBoard.GetKeyDown(Keys.Up)) ChangeCount(1);

        for (int i = 0; i < CountOptions.Length && i < 5; i++)
        {
            if (Input_KeyBoard.GetKeyDown((Keys)((int)Keys.D1 + i)))
            {
                _countIndex = i;
                _rtDirty = true;
            }
        }

        if (Input_Mouse.GetButtonDown(MouseButton.Left))
        {
            Vector2 p = Input_Mouse.Position;
            foreach (Button button in _buttons)
            {
                if (button.Rect.Contains(p))
                {
                    button.Click();
                    break;
                }
            }
        }
    }

    private void ToggleRenderTarget()
    {
        _useRenderTarget = !_useRenderTarget;
        _rtDirty = true;
        _rtRedraws = 0;
    }

    private void ToggleAnimate()
    {
        _animate = !_animate;
        _rtDirty = true;
    }

    private void ChangeCount(int delta)
    {
        int next = Math.Clamp(_countIndex + delta, 0, CountOptions.Length - 1);
        if (next == _countIndex) return;

        _countIndex = next;
        _rtDirty = true;
        _rtRedraws = 0;
    }

    #endregion

    #region 布局

    private void Layout()
    {
        int viewWidth = Device.Viewport.Width;
        int viewHeight = Device.Viewport.Height;

        // 舞台（画面区域）：离屏时它就是 RT 的尺寸，直接绘制时它就是屏幕上的一块区域。
        int width = Math.Clamp(viewWidth - 56, 160, 960);
        int height = Math.Clamp(viewHeight - 320, 160, 620);
        _stage = new Rectangle(Math.Max(28, (viewWidth - width) / 2), 148, width, height);

        // 开关按钮（在舞台上方一排）
        _buttons.Clear();
        int x = _stage.X;
        const int y = 100;
        const int gap = 10;

        x += AddButton(x, y, _useRenderTarget ? "离屏渲染：开 [空格]" : "离屏渲染：关 [空格]", ToggleRenderTarget) + gap;
        x += AddButton(x, y, _animate ? "画面动画：开 [A]" : "画面动画：关 [A]", ToggleAnimate) + gap;
        x += AddButton(x, y, "− [←]", () => ChangeCount(-1)) + gap;
        x += AddButton(x, y, $"精灵数 {CountOptions[_countIndex]:N0} [1-5]", () => ChangeCount(1)) + gap;
        AddButton(x, y, "+ [→]", () => ChangeCount(1));
    }

    private int AddButton(int x, int y, string label, Action click)
    {
        int width = (int)MathF.Ceiling(Font.Measure(label).X) + 28;
        var rect = new Rectangle(x, y, width, 34);
        _buttons.Add(new Button(rect, label, click));
        return width;
    }

    private sealed class Button
    {
        public Button(Rectangle rect, string label, Action click)
        {
            Rect = rect;
            Label = label;
            Click = click;
        }

        public Rectangle Rect { get; }
        public string Label { get; }
        public Action Click { get; }
    }

    #endregion

    #region 绘制

    public override void Draw()
    {
        Layout();

        // 计时必须覆盖「离屏 + 屏幕」整段：只测屏幕阶段的话，开动画时每帧那次离屏重绘会被漏掉，
        // 读数会假得离谱（离屏动画看起来和离屏静止一样快，且远胜直接绘制）。
        _stopwatch.Restart();

        // ① 离屏阶段：必须在 SpriteBatch.Begin 之前完成 ——
        //    Begin 会按“当前是否绑了渲染目标”决定投影矩阵（离屏时 Y 不翻转）。
        if (_useRenderTarget)
            RenderToTargetIfNeeded();

        // ② 屏幕阶段：交给基类 Begin / End（返回按钮 + 标题 + DrawBody）
        //    metrics 只在 Clear 时归零，而切回画布可能会清一次（默认 DiscardContents），
        //    所以基准值一律在「切回之后、屏幕绘制之前」取，统计才与清屏策略无关。
        long beforeScreen = Device.Metrics.SpriteCount;
        double rtMs = _stopwatch.Elapsed.TotalMilliseconds;   // 离屏段（静态且不重绘时为 0）

        base.Draw();
        _stopwatch.Stop();

        // ③ 统计：屏幕阶段 = 本帧总量 − 屏幕阶段开始前的量；离屏阶段 = 离屏那段自己的差值。
        GraphicsMetrics metrics = Device.Metrics;
        _screenSprites = metrics.SpriteCount - beforeScreen;
        _screenDrawCalls = metrics.DrawCount;

        float dt = KTime.unscaledDeltaTime;
        if (dt > 0f) _fps += (1f / dt - _fps) * 0.1f;
        float totalMs = (float)_stopwatch.Elapsed.TotalMilliseconds;
        _rtDrawMs += ((float)rtMs - _rtDrawMs) * 0.1f;
        _screenDrawMs += (totalMs - (float)rtMs - _screenDrawMs) * 0.1f;
        _drawMs += (totalMs - _drawMs) * 0.1f;
    }

    /// <summary>把整片画面画进离屏纹理；内容没变（静态且已画过）就直接复用。</summary>
    private void RenderToTargetIfNeeded()
    {
        EnsureTarget();
        if (_rt == null) return;

        // 动画开着 → 画面每帧都在变，离屏缓存只能每帧重画（这正是“离屏适合静态内容”的原因）。
        if (_animate) _rtDirty = true;
        if (!_rtDirty) return;

        _rtDirty = false;
        _rtRedraws++;

        Device.SetRenderTarget(_rt);
        Device.Clear(new Color(14, 17, 28));

        // 离屏阶段的提交量取差值：metrics 只在本帧 Game 的 Clear 时归零，切回画布不会再重置。
        long before = Device.Metrics.SpriteCount;

        Batch.Begin();
        DrawCluster(Batch, new Rectangle(0, 0, _rt.Width, _rt.Height));
        Batch.End();

        _rtSprites = Device.Metrics.SpriteCount - before;

        Device.SetRenderTarget(null);
    }

    private void EnsureTarget()
    {
        int width = Math.Min(_stage.Width, Device.MaxTextureSize);
        int height = Math.Min(_stage.Height, Device.MaxTextureSize);

        if (_rt != null && (_rt.Width != width || _rt.Height != height))
        {
            // 舞台尺寸变了（窗口缩放）：旧的 RT 连同它的 FBO 一起释放，下面按新尺寸重建。
            _owned.Remove(_rt);
            _rt.Dispose();
            _rt = null;
            _rtRedraws = 0;
        }

        if (_rt == null)
        {
            // RGBA8 + 无深度 + DiscardContents：2D 离屏的默认选择（绑定时自动按 GraphicsDevice.DiscardColor 清屏）。
            _rt = Own(new RenderTarget2D(Device, width, height));
            _rtDirty = true;
            _rtRedraws = 0;
        }
    }

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        // 注意：此处已在 base.Draw() 的 Begin / End 之间，不能再切渲染目标。

        // 全屏底色：默认（DiscardContents）下切回画布会按 GraphicsDevice.DiscardColor 清一次，
        // 离屏模式若不自己铺满背景，画布就会露出那次清屏的颜色（照 MonoGame 也是这个行为）。
        DrawRect(batch, new Rectangle(0, 0, Device.Viewport.Width, Device.Viewport.Height), new Color(10, 12, 20));

        // 背景框
        DrawRect(batch, _stage, new Color(18, 22, 34));

        if (_useRenderTarget && _rt != null)
        {
            // 离屏：整片画面 = 1 个精灵
            batch.Draw(_rt, _stage, Color.White);
        }
        else if (_ball != null)
        {
            // 直接绘制：每帧提交全部精灵
            DrawCluster(batch, _stage);
        }

        // 按钮
        foreach (Button button in _buttons)
        {
            bool hover = button.Rect.Contains(Input_Mouse.Position);
            DrawRect(batch, button.Rect, hover ? new Color(40, 62, 104) : new Color(26, 32, 48));
            batch.DrawString(Font, button.Label, new Vector2(button.Rect.X + 14, button.Rect.Y + 7),
                             hover ? Color.White : new Color(170, 200, 240));
        }

        DrawPanel(batch);
    }

    /// <summary>画那一片「几千个精灵」：彩虹螺旋。area 为绘制区域（离屏时是整张 RT，直绘时是屏幕上的舞台）。</summary>
    private void DrawCluster(SpriteBatch batch, Rectangle area)
    {
        Texture2D ball = _ball!;
        int count = CountOptions[_countIndex];

        float cx = area.X + area.Width / 2f;
        float cy = area.Y + area.Height / 2f;
        float maxRadius = MathF.Min(area.Width, area.Height) / 2f - 6f;
        float spin = _animate ? KTime.unscaledTime * 0.5f : 0f;
        float pulse = _animate ? 0.86f + 0.14f * MathF.Sin(KTime.unscaledTime * 0.9f) : 1f;
        float hueShift = _animate ? KTime.unscaledTime * 40f : 0f;

        // 精灵尺寸随数量自适应，保证几千个也有画面而不是糊成一片
        float pixelSize = Math.Clamp(MathF.Sqrt(area.Width * (float)area.Height / count) * 1.1f, 3f, 26f);
        var scale = new Vector2(pixelSize / ball.Width, pixelSize / ball.Height);
        var originOffset = new Vector2(ball.Width / 2f, ball.Height / 2f);

        const float turns = 16f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float angle = t * turns * MathF.PI * 2f + spin;
            float radius = MathF.Sqrt(t) * maxRadius * pulse;
            float x = cx + MathF.Cos(angle) * radius;
            float y = cy + MathF.Sin(angle) * radius;

            batch.Draw(ball, new Vector2(x, y), null,
                       Hsv((t * 360f + hueShift) % 360f, 0.85f, 1f),
                       0f, originOffset, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>底部数据面板：FPS / 提交量 / 离屏状态。</summary>
    private void DrawPanel(SpriteBatch batch)
    {
        int count = CountOptions[_countIndex];
        float lineHeight = Font.LineSpacing + 4f;
        float panelHeight = lineHeight * 6f + 12f;

        float y = _stage.Bottom + 10f;
        if (y + panelHeight > Device.Viewport.Height - 8f)
            y = Math.Max(_stage.Y + 8f, Device.Viewport.Height - 8f - panelHeight);

        float x = _stage.X;

        y += DrawLine(batch, Font,
            $"FPS {_fps:F1}    绘制耗时 {_drawMs:F2} ms（离屏 {_rtDrawMs:F2} + 屏幕 {_screenDrawMs:F2}）"
            + $"    屏幕阶段精灵 {_screenSprites:N0}    DrawCall {_screenDrawCalls:N0}",
            new Vector2(x, y), new Color(126, 200, 255));

        if (_useRenderTarget && _rt != null)
        {
            y += DrawLine(batch, Font,
                $"离屏：开     RT {_rt.Width}×{_rt.Height}     已重绘 {_rtRedraws} 次     上次重绘 {_rtSprites:N0} 个精灵",
                new Vector2(x, y), new Color(150, 255, 180));
            y += DrawLine(batch, Font,
                _animate
                    ? "画面在动 → 离屏纹理每帧重画 N 个精灵再贴一遍（N+1，多一次全屏贴图）："
                      + "上面「离屏」那一段就是多出来的开销，看 FPS 比看耗时更准。"
                    : "画面静止 → 只在第一帧画一次，之后每帧只贴 1 个精灵：几千个精灵退化成 1 个。",
                new Vector2(x, y), new Color(200, 200, 210));
        }
        else
        {
            y += DrawLine(batch, Font,
                $"离屏：关     每帧都要把 {count:N0} 个精灵全部提交一次（顶点变换 + 批次上传）",
                new Vector2(x, y), new Color(255, 190, 150));
            y += DrawLine(batch, Font,
                "开一下空格就能看出差别：同一片画面，离屏后每帧只剩 1 个精灵 + 1 次贴图。",
                new Vector2(x, y), new Color(200, 200, 210));
        }

        y += DrawLine(batch, Font,
            "用法：device.SetRenderTarget(rt) → Clear → batch.Begin…End → device.SetRenderTarget(null)，"
            + "然后 rt 就是一张普通纹理（顺序反了会画到画布上 / 上下颠倒）。",
            new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font,
            "空格 = 离屏开关    A = 画面动画    ← → / 1..5 = 精灵数量    Esc = 返回",
            new Vector2(x, y), new Color(150, 165, 190));
    }

    #endregion

    #region 资源

    /// <summary>程序化生成一张带光照 + 高光的球（白色，绘制时用 Color 着色）。</summary>
    private static Texture2D MakeBallTexture(GraphicsDevice device, int size)
    {
        byte[] pixels = new byte[size * size * 4];

        // 光方向（左上前）与 Blinn 半程向量
        var light = Vector3.Normalize(new Vector3(-0.45f, -0.55f, 0.70f));
        var half = Vector3.Normalize(light + new Vector3(0f, 0f, 1f));

        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - radius) / radius;
                float ny = (y + 0.5f - radius) / radius;
                float dist = MathF.Sqrt(nx * nx + ny * ny);

                // 边缘一个像素的抗锯齿
                float alpha = Math.Clamp((1f - dist) * size * 0.5f, 0f, 1f);

                float nz = MathF.Sqrt(MathF.Max(0f, 1f - MathF.Min(dist, 1f) * MathF.Min(dist, 1f)));
                var normal = Vector3.Normalize(new Vector3(nx, ny, nz));

                float diffuse = MathF.Max(0f, Vector3.Dot(normal, light));
                float specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, half)), 28f);
                float v = 0.28f + 0.68f * diffuse + 0.85f * specular;

                int offset = (y * size + x) * 4;
                byte c = (byte)Math.Clamp(v * 255f, 0f, 255f);
                pixels[offset + 0] = c;
                pixels[offset + 1] = c;
                pixels[offset + 2] = c;
                pixels[offset + 3] = (byte)(alpha * 255f);
            }
        }

        return device.CreateTexture(size, size, pixels, SurfaceFormat.Color);
    }

    private static Color Hsv(float h, float s, float v)
    {
        float c = v * s;
        float x = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
        float m = v - c;
        float r = 0f, g = 0f, b = 0f;

        if (h < 60f) { r = c; g = x; }
        else if (h < 120f) { r = x; g = c; }
        else if (h < 180f) { g = c; b = x; }
        else if (h < 240f) { g = x; b = c; }
        else if (h < 300f) { r = x; b = c; }
        else { r = c; b = x; }

        return new Color((int)((r + m) * 255f), (int)((g + m) * 255f), (int)((b + m) * 255f));
    }

    private T Own<T>(T resource) where T : IDisposable
    {
        _owned.Add(resource);
        return resource;
    }

    public override void Dispose()
    {
        foreach (IDisposable resource in _owned) resource.Dispose();
        _owned.Clear();
        _rt = null;
        base.Dispose();
    }

    #endregion
}
