using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{

    /// <summary>
    /// 渲染目标（RenderTarget2D）专项：顶部按钮<b>切换显示画面</b>，一次只显示一个画面。
    /// <list type="bullet">
    ///   <item><description><b>测试离屏</b>：内容画进离屏1，再把它当纹理贴到屏幕（离屏基本通路）。</description></item>
    ///   <item><description><b>测试离屏 MSAA</b>：MSAA=0 与 =4 并排，并说明"画布 MSAA"与"离屏 MSAA"在
    ///   WebGL / WebGPU 下的差别。</description></item>
    ///   <item><description><b>测试 RT→RT 合成</b>：离屏1 与离屏2 一起画进合成目标
    ///   （传奇的地板图 + 光照图合成就是这一步）。</description></item>
    ///   <item><description><b>测试嵌套（画布↔RT）</b>：先在画布画 → 切离屏 → 切回继续画，
    ///   看画布上先前的内容还在不在（传奇每帧的用法）。</description></item>
    ///   <item><description><b>测试累积</b>：只在首帧清一次，之后每帧加一条横杠，
    ///   验证 <c>PreserveContents</c> 是否真的保留内容。</description></item>
    /// </list>
    /// </summary>
    public sealed class OffscreenScene : DemoScene
    {
        public override string Title => "3) 渲染目标专项：离屏 / MSAA / 合成 / 嵌套 / 累积";

        protected override string Description
            => "点上方按钮切换显示画面（Esc 返回总纲）";

        protected override string Hint => string.Empty;

        /// <summary>显示画面。</summary>
        private enum Screen
        {
            Offscreen,
            Msaa,
            Composite,
            Nested,
            Accumulate,
        }

        private static readonly (string Name, Screen Value)[] Screens =
        [
            ("测试离屏", Screen.Offscreen),
            ("测试离屏MSAA", Screen.Msaa),
            ("测试RT→RT合成", Screen.Composite),
            ("测试来回切(屏幕↔离屏)", Screen.Nested),
            ("测试累积", Screen.Accumulate),
        ];

        private const int RtW = 320;
        private const int RtH = 200;

        private const int BtnH = 30;
        private const int BtnGap = 8;
        private const int BodyTop = 118;
        private const int BarsTotal = 25;

        // 流水线式对照区：4 列 × 2 行。按最窄的画布（约 1032）留余量：4*220 + 3*20 + 28 = 968。
        private const int PanelW = 220;
        private const int PanelH = 138;     // 保持 320:200 的比例（1.6:1）
        private const int PanelGap = 20;

        private Screen _screen = Screen.Offscreen;
        private readonly List<Rectangle> _buttons = [];

        private RenderTarget2D? _rt1;      // 离屏1（无 MSAA）
        private RenderTarget2D? _rt2;      // 离屏2（无 MSAA）
        private RenderTarget2D? _rtMsaa;   // 离屏 MSAA=4
        private RenderTarget2D? _rtComp;   // 合成目标
        private RenderTarget2D? _rtA;      // A：① 阶段单独画出来（背景 + 红块）
        private RenderTarget2D? _rtC;      // C：③ 阶段单独画出来（绿块）
        private RenderTarget2D? _rtRef;    // 参考：不切目标，一次把 ①②③ 画完（应有的样子）
        private RenderTarget2D? _rtMix;    // 合成：把 A + B + C 依次合成进一张
        private RenderTarget2D? _rtE;      // 实际(离屏)：同样的顺序，但切走再切回同一个目标继续画
        private RenderTarget2D? _accum;    // 累积

        // 首次 RenderOffscreen 时创建（离屏阶段早于任何 Draw），故声明为非空。
        private Texture2D _ball = null!;      // 带高光的球：曲线边缘，测 MSAA 最直观
        private Texture2D _bgCool = null!;    // 冷色渐变底
        private Texture2D _bgWarm = null!;    // 暖色渐变底
        private int _frame;
        private bool _accumSeeded;

        protected override void UpdateBody()
        {
            _frame++;
            HandleButtons();
        }

        // ================================================================
        // 顶部按钮：切换显示画面（不是开关）
        // ================================================================

        private void LayoutButtons()
        {
            _buttons.Clear();

            float x = 28f;
            for (int i = 0; i < Screens.Length; i++)
            {
                int w = 28 + EstimateWidth(Screens[i].Name);
                _buttons.Add(new Rectangle((int)x, 76, w, BtnH));
                x += w + BtnGap;
            }
        }

        /// <summary>按文字长度估按钮宽度（字号 20：CJK 约 20px/字，西文约 10px）。</summary>
        private static int EstimateWidth(string s)
        {
            int w = 0;
            foreach (char c in s) w += c > 0x2E80 ? 20 : 10;
            return w;
        }

        private void HandleButtons()
        {
            LayoutButtons();

            if (!Input_Mouse.GetButtonDown(MouseButton.Left)) return;

            Vector2 p = Input_Mouse.Position;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (!_buttons[i].Contains(p)) continue;

                if (_screen != Screens[i].Value)
                {
                    _screen = Screens[i].Value;

                    // 嵌套模式要保住画布上已画的内容：与传奇 DXManager 的做法一致
                    //（PresentationParameters 默认是 DiscardContents，切回画布本来就会清屏）。
                    Device.PresentationParameters.RenderTargetUsage = _screen == Screen.Nested
                        ? RenderTargetUsage.PreserveContents
                        : RenderTargetUsage.DiscardContents;
                }
                return;
            }
        }

        // ================================================================
        // 离屏阶段（在 Begin / End 之外：切换渲染目标必须发生在批次之外）
        // ================================================================

        protected override void RenderOffscreen(SpriteBatch batch)
        {
            if (_ball is null)
            {
                _ball = MakeBall(64);
                _bgCool = MakeVGradient(4, 64, new Color(22, 30, 48), new Color(10, 13, 22));
                _bgWarm = MakeVGradient(4, 64, new Color(48, 30, 26), new Color(20, 13, 14));
            }

            // 各画面的目标按需创建（MSAA 只能在创建时指定，故 MSAA 版是另一张 RT）。
            switch (_screen)
            {
                case Screen.Offscreen:
                    _rt1 ??= NewTarget(0);
                    DrawInto(_rt1, batch, kind: 0);
                    break;

                case Screen.Msaa:
                    _rt1 ??= NewTarget(0);
                    _rtMsaa ??= NewTarget(4);
                    // 注意：MSAA 画面不用球阵 —— 球的圆边是【纹理 alpha】形成的，MSAA 管不到它，
                    // 用球看 MSAA 开没开等于白测。这里改画旋转的实心色块（几何边缘）。
                    DrawEdgeInto(_rt1, batch);
                    DrawEdgeInto(_rtMsaa, batch);
                    break;

                case Screen.Composite:
                    _rt1 ??= NewTarget(0);
                    _rt2 ??= NewTarget(0);
                    _rtComp ??= NewTarget(0);
                    DrawInto(_rt1, batch, kind: 0);
                    DrawInto(_rt2, batch, kind: 1);
                    DrawComposite(batch);
                    break;

                case Screen.Nested:
                    _rt1 ??= NewTarget(0);      // B（透明底，供各处合成用）
                    _rtA ??= NewTarget(0);
                    _rtC ??= NewTarget(0);
                    _rtRef ??= NewTarget(0);
                    _rtMix ??= NewTarget(0);
                    _rtE ??= NewTarget(0, RenderTargetUsage.PreserveContents);

                    // —— A / B / C：三个阶段各自单独画出来的离屏 ——
                    RenderStageAlone(_rtA, batch, 0);
                    RenderStageTransparent(_rt1, batch, 1);
                    RenderStageAlone(_rtC, batch, 2);

                    // —— 参考：不切换目标，一口气把 ①②③ 画完（应有的样子） ——
                    Device.SetRenderTarget(_rtRef);
                    Device.Clear(new Color(12, 15, 24));
                    batch.Begin();
                    DrawStage(batch, 0, 0, 0, RtW, RtH);
                    DrawStage(batch, 1, 0, 0, RtW, RtH);
                    DrawStage(batch, 2, 0, 0, RtW, RtH);
                    batch.End();
                    Device.SetRenderTarget(null);

                    // —— 合成：A → B → C 依次叠进一张（全程在同一张离屏内，不切走再切回） ——
                    Device.SetRenderTarget(_rtMix);
                    Device.Clear(new Color(12, 15, 24));
                    batch.Begin();
                    DrawStage(batch, 0, 0, 0, RtW, RtH);
                    batch.Draw(_rt1, new Rectangle(0, 0, RtW, RtH), Color.White);
                    DrawStage(batch, 2, 0, 0, RtW, RtH);
                    batch.End();
                    Device.SetRenderTarget(null);

                    // —— 实际(离屏)：同样顺序，但中间要【切走再切回同一个目标】 ——
                    Device.SetRenderTarget(_rtE);
                    Device.Clear(new Color(12, 15, 24));
                    batch.Begin();
                    DrawStage(batch, 0, 0, 0, RtW, RtH);      // ①
                    batch.End();

                    Device.SetRenderTarget(_rt1);              // ② 切去画 B
                    Device.Clear(new Color(0, 0, 0, 0));
                    batch.Begin();
                    DrawStage(batch, 1, 0, 0, RtW, RtH);
                    batch.End();

                    Device.SetRenderTarget(_rtE);              // ③ 切回 E，接着画
                    batch.Begin();
                    batch.Draw(_rt1, new Rectangle(0, 0, RtW, RtH), Color.White);
                    DrawStage(batch, 2, 0, 0, RtW, RtH);
                    batch.End();
                    Device.SetRenderTarget(null);

                    // —— 实际(画布)：① 先画在真实屏幕上，再切离屏画 ②，切回后由 DrawBody 补 ③ ——
                    batch.Begin();
                    DrawStage(batch, 0, PanelX(1), Row2Y, PanelW, PanelH);
                    batch.End();

                    Device.SetRenderTarget(_rt1);              // ② 切去离屏
                    Device.Clear(new Color(0, 0, 0, 0));
                    batch.Begin();
                    DrawStage(batch, 1, 0, 0, RtW, RtH);
                    batch.End();
                    Device.SetRenderTarget(null);              // 切回屏幕
                    break;

                case Screen.Accumulate:
                    _accum ??= NewTarget(0, RenderTargetUsage.PreserveContents);
                    Accumulate(batch);
                    break;
            }
        }

        private static RenderTarget2D NewTarget(int multiSampleCount,
                                                RenderTargetUsage usage = RenderTargetUsage.DiscardContents)
            => new(KSceneMgr.Game.GraphicsDevice, RtW, RtH, false,
                   SurfaceFormat.Color, DepthFormat.None, multiSampleCount, usage);

        /// <summary>把一种内容画进目标：kind=0 冷色底+球阵，kind=1 暖色底+球阵（便于区分两张离屏）。</summary>
        private void DrawInto(RenderTarget2D? target, SpriteBatch batch, int kind)
        {
            if (target is null) return;

            const int cols = 6;
            const int rows = 4;
            const float cell = 50f;
            const float ballScale = 0.78f;

            Device.SetRenderTarget(target);
            Device.Clear(kind == 0 ? new Color(12, 16, 26) : new Color(26, 16, 20));

            batch.Begin();

            // ① 柔和的纵向渐变底（不抢眼，也不干扰对球边缘的观察）
            batch.Draw(kind == 0 ? _bgCool : _bgWarm, new Rectangle(0, 0, RtW, RtH), Color.White);

            // ② 彩色球阵：色相随时间流动 + 轻微呼吸缩放，画面不至于呆板
            float originX = (RtW - (cols - 1) * cell) * 0.5f;
            float originY = (RtH - (rows - 1) * cell) * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float hue = (c * 34f + r * 22f + _frame * 0.5f) % 360f;
                    float pulse = 1f + 0.07f * MathF.Sin((_frame + c * 4 + r * 6) * 0.05f);
                    batch.Draw(_ball,
                        new Vector2(originX + c * cell, originY + r * cell),
                        Hsv(hue, 0.55f, 1f),
                        0f, new Vector2(32f, 32f), ballScale * pulse);
                }
            }

            // ③ 四角方位标记：小的纯色块，只在角落，用来确认离屏没有翻转
            DrawCornerMarks(batch, RtW, RtH, 10);

            batch.End();

            Device.SetRenderTarget(null);
        }

        /// <summary>四角方位标记（左上红 / 右上绿 / 左下蓝 / 右下黄）。上下翻转或左右镜像会立刻暴露。</summary>
        private void DrawCornerMarks(SpriteBatch batch, int w, int h, int m)
        {
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(2, 2, m, m), new Color(220, 70, 70));
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(w - m - 2, 2, m, m), new Color(70, 200, 120));
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(2, h - m - 2, m, m), new Color(70, 140, 220));
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(w - m - 2, h - m - 2, m, m), new Color(230, 200, 80));
        }

        /// <summary>
        /// MSAA 专用内容：全部由<b>旋转的实心色块</b>构成。
        /// <para>
        /// 关键：MSAA 抗的是<b>三角形（几何）边缘</b>；球体那种靠纹理 alpha 形成的圆边不在它的作用范围内，
        /// 拿球去看 MSAA 开没开等于白测 —— 这正是上一版"MSAA 貌似没起作用"的原因。
        /// 做法照 Test.Common 的 <c>MSAATestScene.DrawScene</c>。
        /// </para>
        /// </summary>
        private void DrawEdgeInto(RenderTarget2D? target, SpriteBatch batch)
        {
            if (target is null) return;

            Device.SetRenderTarget(target);
            Device.Clear(new Color(14, 18, 28));

            float spin = _frame * 0.004f;

            batch.Begin();
            DrawEdgeScene(batch, RtW, RtH, spin);
            DrawCornerMarks(batch, RtW, RtH, 10);
            batch.End();

            Device.SetRenderTarget(null);
        }

        private static void DrawEdgeScene(SpriteBatch batch, int w, int h, float spin)
        {
            float cx = w / 2f, cy = h / 2f;

            // 一组从中心射出的细棒：旋转时边缘阶梯最明显
            const int bars = 48;
            for (int i = 0; i < bars; i++)
            {
                float a = i / (float)bars * MathF.PI * 2f + spin;
                float len = MathF.Min(w, h) * 0.47f;
                batch.Draw(KDefaultRes.DefaultTexture2D, new Vector2(cx, cy), null,
                    Hsv((i * 360f / bars + spin * 40f) % 360f, 0.85f, 1f), a,
                    new Vector2(0.5f, 0.5f), new Vector2(len, 9f), SpriteEffects.None, 0f);
            }

            // 几个旋转方块，强化边缘锯齿观感
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * MathF.PI + spin * 0.7f;
                float s = 44f + i * 18f;
                batch.Draw(KDefaultRes.DefaultTexture2D, new Vector2(cx, cy), null,
                    Hsv((i * 70f) % 360f, 0.6f, 1f), a,
                    new Vector2(0.5f, 0.5f), new Vector2(s, s), SpriteEffects.None, 0f);
            }
        }

        /// <summary>把离屏1 与离屏2 一起画进合成目标（离屏之间的合成）。</summary>
        private void DrawComposite(SpriteBatch batch)
        {
            if (_rtComp is null || _rt1 is null || _rt2 is null) return;

            Device.SetRenderTarget(_rtComp);
            Device.Clear(new Color(8, 10, 16));

            batch.Begin();
            batch.Draw(_rt1, new Rectangle(0, 0, RtW, RtH), Color.White);                       // 离屏1 铺满
            batch.Draw(_rt2, new Rectangle(RtW / 2, RtH / 2, RtW / 2, RtH / 2), Color.White);   // 离屏2 叠在右下
            batch.End();

            Device.SetRenderTarget(null);
        }

        /// <summary>
        /// 累积：只在首帧清一次，之后每帧加一条横杠。
        /// 铺满＝内容跨帧保留（通过）；只剩一条并不断跳位（看着像闪烁）＝每次切进 RT 都被清屏；全黑＝RT 不可用。
        /// </summary>
        private void Accumulate(SpriteBatch batch)
        {
            if (_accum is null) return;

            Device.SetRenderTarget(_accum);

            bool seed = !_accumSeeded;
            if (seed)
            {
                Device.Clear(new Color(10, 14, 24));
                _accumSeeded = true;
            }

            batch.Begin();
            int barH = RtH / BarsTotal;

            // 【决定性判据】只在首帧画一条红杠。它若一直看得见，说明纹理内容真的跨帧保留了
            //（load 生效），"铺不满"就得往别处查；若它消失、只剩黄杠，说明每次切进 RT 都被清屏。
            if (seed)
                batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(0, 0, RtW, 6), new Color(220, 70, 70));

            // 每帧画一条黄杠，y 逐帧下移（(帧 % 25) * 8），保留得住就会 25 帧内铺满。
            batch.Draw(KDefaultRes.DefaultTexture2D,
                new Rectangle(0, (_frame % BarsTotal) * barH, RtW, barH - 1), new Color(255, 200, 90));
            batch.End();

            Device.SetRenderTarget(null);
        }

        /// <summary>对照区的 x（0..2）。</summary>
        private static float PanelX(int index) => 28f + index * (PanelW + PanelGap);

        /// <summary>第二行的 y（D / E 所在行）。</summary>
        private static float Row2Y => BodyTop + PanelH + 36f;

        /// <summary>把一个阶段单独画进离屏（不透明底），用来当"这一段长什么样"的参考。</summary>
        private void RenderStageAlone(RenderTarget2D target, SpriteBatch batch, int stage)
        {
            Device.SetRenderTarget(target);
            Device.Clear(new Color(12, 15, 24));
            batch.Begin();
            DrawStage(batch, stage, 0, 0, RtW, RtH);
            batch.End();
            Device.SetRenderTarget(null);
        }

        /// <summary>把一个阶段画进离屏（<b>透明底</b>）：合成到别处时不会盖住底下已画的内容。</summary>
        private void RenderStageTransparent(RenderTarget2D target, SpriteBatch batch, int stage)
        {
            Device.SetRenderTarget(target);
            Device.Clear(new Color(0, 0, 0, 0));
            batch.Begin();
            DrawStage(batch, stage, 0, 0, RtW, RtH);
            batch.End();
            Device.SetRenderTarget(null);
        }

        /// <summary>
        /// 画一个阶段的内容。三个阶段叠起来才是完整画面，分开画才能看出"切回来时少了几段"：
        /// <list type="bullet">
        ///   <item><description>0：背景 + 顶部红色标记（切去离屏<b>之前</b>画在屏幕上的）</description></item>
        ///   <item><description>1：球阵（在<b>离屏</b>里画的，透明底）</description></item>
        ///   <item><description>2：底部绿色标记（<b>切回</b>屏幕之后接着画的）</description></item>
        /// </list>
        /// 三个阶段的相对布局固定，故画在离屏(0,0,RtW,RtH) 与画在屏幕某块区域 的构图完全一致，可直接比对。
        /// </summary>
        private void DrawStage(SpriteBatch batch, int stage, float x, float y, float w, float h)
        {
            switch (stage)
            {
                case 0:
                    batch.Draw(_bgCool, new Rectangle((int)x, (int)y, (int)w, (int)h), Color.White);
                    // 红块贴着顶部：合成图内缩后，顶部这条边缘带仍能看到它们
                    for (int i = 0; i < 4; i++)
                        batch.Draw(KDefaultRes.DefaultTexture2D,
                            new Rectangle((int)(x + 8 + i * 30), (int)(y + 3), 18, 18), new Color(220, 70, 70));
                    break;

                case 1:
                    for (int r = 0; r < 2; r++)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            float hue = (c * 45f + r * 30f + _frame * 0.5f) % 360f;
                            batch.Draw(_ball,
                                new Vector2(x + w * 0.24f + c * w * 0.19f, y + h * 0.46f + r * h * 0.22f),
                                Hsv(hue, 0.55f, 1f), 0f, new Vector2(32f, 32f), 0.62f);
                        }
                    }
                    break;

                case 2:
                    // 绿块贴着底部：与红块分居上下，便于一眼看出"少了哪一段"
                    for (int i = 0; i < 4; i++)
                        batch.Draw(KDefaultRes.DefaultTexture2D,
                            new Rectangle((int)(x + 8 + i * 30), (int)(y + h - 21), 18, 18), new Color(70, 200, 120));
                    break;
            }
        }

        // ================================================================
        // 屏幕绘制
        // ================================================================

        /// <summary>嵌套画面判定带的 y（在面板下方，不会被任何面板盖住）。</summary>
        private float NestedVerdictY => BodyTop + 260f + 40f;

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            // 顶部按钮：当前画面高亮
            for (int i = 0; i < _buttons.Count; i++)
            {
                Rectangle rect = _buttons[i];
                bool current = Screens[i].Value == _screen;
                bool hover = rect.Contains(Input_Mouse.Position);

                batch.Draw(KDefaultRes.DefaultTexture2D, rect,
                    current ? new Color(58, 92, 150) : hover ? new Color(52, 76, 120) : new Color(34, 44, 68));
                batch.DrawString(Font, Screens[i].Name, new Vector2(rect.X + 12f, rect.Y + 5f),
                    current ? Color.White : new Color(190, 208, 235));
            }

            float x = 28f;

            switch (_screen)
            {
                case Screen.Offscreen:
                    if (_rt1 is null) return;
                    Panel(batch, _rt1, x, BodyTop, "离屏1 的内容（当作普通纹理贴到屏幕）");
                    float y = BodyTop + 260f;
                    y = DrawLine(batch, "判定：彩色球阵的高光应在每个球的【左上】；四角标记为 左上红 / 右上绿 / 左下蓝 / 右下黄",
                        x, y, new Color(255, 206, 110));
                    DrawLine(batch, "（高光跑到左下、或四角标记上下颠倒，说明离屏的投影翻转取错了 —— 这是后端相关的）",
                        x, y, new Color(150, 165, 195));
                    break;

                case Screen.Msaa:
                    if (_rt1 is null || _rtMsaa is null) return;
                    Panel(batch, _rt1, x, BodyTop, "离屏 MSAA=0（边缘带锯齿）");
                    Panel(batch, _rtMsaa, x + RtW + 48f, BodyTop, "离屏 MSAA=4（通道结束时自动解析）");
                    float my = BodyTop + 260f;
                    my = DrawLine(batch, "判定：右侧细棒/方块的斜边应明显比左侧平滑（阶梯感消失）", x, my, new Color(255, 206, 110));
                    my = DrawLine(batch, "注意：球的圆边是图片自带的透明边，MSAA 管不到 —— 故本画面改用旋转色块（真正的图形边缘）",
                        x, my, new Color(150, 165, 195));
                    my = DrawLine(batch, "【画布 MSAA】WebGL：getContext({antialias}) 建上下文时定死，之后改不了；",
                        x, my, new Color(150, 165, 195));
                    my = DrawLine(batch, "            WebGPU：画布配置根本没有 antialias 项，交换链恒为单采样，",
                        x, my, new Color(150, 165, 195));
                    my = DrawLine(batch, "                    要 MSAA 必须自建多重采样纹理 + resolveTarget 解析。",
                        x, my, new Color(150, 165, 195));
                    DrawLine(batch, "【离屏 MSAA】两个后端都能随时创建/销毁（WebGL 是 multisample renderbuffer，WebGPU 是 sampleCount>1 的纹理），与画布互不相干。",
                        x, my, new Color(150, 165, 195));
                    break;

                case Screen.Composite:
                    if (_rt1 is not { } c1 || _rt2 is not { } c2 || _rtComp is not { } cComp) return;
                    Panel(batch, c1, PanelX(0), BodyTop, "离屏1");
                    Panel(batch, c2, PanelX(1), BodyTop, "离屏2");
                    Panel(batch, cComp, PanelX(2), BodyTop, "合成结果（1 铺满 + 2 叠右下）");
                    float cy = BodyTop + 260f;
                    cy = DrawLine(batch, "判定：合成图应同时含离屏1 的冷色底球阵与离屏2 的暖色底球阵（右下角叠加）",
                        x, cy, new Color(255, 206, 110));
                    DrawLine(batch, "这一步是传奇「地板图 + 光照图 → 合成」的等价物：离屏之间的合成，不是离屏到画布。",
                        x, cy, new Color(150, 165, 195));
                    break;

                case Screen.Nested:
                    if (_rt1 is not { } rt1 || _rtA is not { } rtA || _rtC is not { } rtC
                        || _rtRef is not { } rtRef || _rtMix is not { } rtMix || _rtE is not { } rtE) return;

                    // 第一行：参考 + 三个阶段各自的离屏
                    Panel(batch, rtRef, PanelX(0), BodyTop, "参考：一次画完");
                    Panel(batch, rtA, PanelX(1), BodyTop, "A ① 背景+红块");
                    Panel(batch, rt1, PanelX(2), BodyTop, "B ② 球阵(透明底)");
                    Panel(batch, rtC, PanelX(3), BodyTop, "C ③ 绿块");

                    // 第二行：合成 → 实际(画布) → 实际(离屏)
                    Panel(batch, rtMix, PanelX(0), Row2Y, "合成：A+B+C");

                    // 实际(画布)：① 已在离屏阶段画在真实屏幕上，这里补 ③ 合成 + 绿块
                    batch.Draw(rt1, new Rectangle((int)PanelX(1), (int)Row2Y, PanelW, PanelH), Color.White);
                    DrawStage(batch, 2, PanelX(1), Row2Y, PanelW, PanelH);
                    Frame(batch, PanelX(1), Row2Y, PanelW, PanelH, new Color(255, 206, 110));
                    batch.DrawString(Font, "实际(画布)：屏幕↔离屏",
                        new Vector2(PanelX(1), Row2Y + PanelH + 6f), new Color(255, 206, 110));

                    Panel(batch, rtE, PanelX(2), Row2Y, "实际(离屏)：切走再切回");

                    float ny = Row2Y + PanelH + 34f;
                    ny = DrawLine(batch, "【应当一致】参考 = 合成 = 实际(画布) = 实际(离屏)：背景 + 顶部红块 + 球阵 + 底部绿块",
                        x, ny, new Color(255, 206, 110));
                    ny = DrawLine(batch, "【关键判定】实际(离屏)有红块、实际(画布)没有 → 切换逻辑没问题，是【画布】保不住内容",
                        x, ny, new Color(255, 206, 110));
                    ny = DrawLine(batch, "            两个都没有红块 → 是【切走再切回同一目标继续画】这条逻辑的问题",
                        x, ny, new Color(255, 150, 150));
                    ny = DrawLine(batch, "好比画画：先在纸上画背景(A) → 去另一张纸画球(B) → 回来补画绿块(C)。回来时背景还在吗？",
                        x, ny, new Color(150, 165, 195));
                    DrawLine(batch, "传奇每帧都这么来回切很多次。本画面已设为「保留内容」模式（默认「丢弃内容」会清屏，属设计行为不是 bug）。",
                        x, ny, new Color(150, 165, 195));
                    break;

                case Screen.Accumulate:
                    if (_accum is null) return;
                    Panel(batch, _accum, x, BodyTop, "累积（首帧清一次，之后每帧加一条横杠）");
                    float ay = BodyTop + 260f;
                    ay = DrawLine(batch, "黄杠：每帧画一条、位置逐帧下移。留得住的话，25 帧内会铺满并一直保持",
                        x, ay, new Color(255, 206, 110));
                    ay = DrawLine(batch, "红杠：只在第一帧画过一次。它还在 ＝ 离屏里的内容能跨帧留住（正常）",
                        x, ay, new Color(255, 150, 150));
                    ay = DrawLine(batch, "        红杠没了、只剩黄杠 ＝ 每次切进离屏都被清掉重来；整块全黑 ＝ 离屏根本没画上",
                        x, ay, new Color(255, 150, 150));
                    DrawLine(batch, $"帧: {_frame}    本帧黄杠位置 y: {(_frame % BarsTotal) * (RtH / BarsTotal)}",
                        x, ay, new Color(150, 165, 195));
                    break;
            }
        }

        /// <summary>画一块面板（RT 内容 + 下方标题）。</summary>
        private void Panel(SpriteBatch batch, Texture2D texture, float x, float y, string caption)
        {
            batch.Draw(texture, new Rectangle((int)x, (int)y, PanelW, PanelH), Color.White);
            batch.DrawString(Font, caption, new Vector2(x, y + PanelH + 6f), new Color(150, 165, 195));
        }

        /// <summary>给一块区域描一圈 2px 边框（用来标出"这块是直接画在屏幕上的"）。</summary>
        private void Frame(SpriteBatch batch, float x, float y, float w, float h, Color color)
        {
            int ix = (int)x, iy = (int)y, iw = (int)w, ih = (int)h, t = 2;
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy, iw, t), color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy + ih - t, iw, t), color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy, t, ih), color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix + iw - t, iy, t, ih), color);
        }
    }

}
