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
            ("测试嵌套(画布↔RT)", Screen.Nested),
            ("测试累积", Screen.Accumulate),
        ];

        private const int RtW = 320;
        private const int RtH = 200;

        private const int BtnH = 30;
        private const int BtnGap = 8;
        private const int BodyTop = 118;
        private const int BarsTotal = 25;

        private Screen _screen = Screen.Offscreen;
        private readonly List<Rectangle> _buttons = [];

        private RenderTarget2D? _rt1;      // 离屏1（无 MSAA）
        private RenderTarget2D? _rt2;      // 离屏2（无 MSAA）
        private RenderTarget2D? _rtMsaa;   // 离屏 MSAA=4
        private RenderTarget2D? _rtComp;   // 合成目标
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
                    _rt1 ??= NewTarget(0);
                    DrawCanvasBefore(batch);      // ① 先在画布上画
                    DrawInto(_rt1, batch, kind: 0);   // ② 切离屏画，末尾切回画布
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

        private void DrawCanvasBefore(SpriteBatch batch)
        {
            float y = NestedVerdictY;

            batch.Begin();
            for (int i = 0; i < 5; i++)
                batch.Draw(KDefaultRes.DefaultTexture2D,
                    new Rectangle(28 + i * 44, (int)y, 36, 36), new Color(220, 70, 70));
            batch.End();
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
                    my = DrawLine(batch, "注意：球的圆边是【纹理 alpha】形成的，MSAA 抗不到它 —— 故本画面改用旋转实心块（几何边缘）",
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
                    if (_rt1 is null || _rt2 is null || _rtComp is null) return;
                    Panel(batch, _rt1, x, BodyTop, "离屏1");
                    Panel(batch, _rt2, x + RtW + 48f, BodyTop, "离屏2");
                    Panel(batch, _rtComp, x + 2f * (RtW + 48f), BodyTop, "合成结果（1 铺满 + 2 叠右下）");
                    float cy = BodyTop + 260f;
                    cy = DrawLine(batch, "判定：合成图应同时含离屏1 的冷色底球阵与离屏2 的暖色底球阵（右下角叠加）",
                        x, cy, new Color(255, 206, 110));
                    DrawLine(batch, "这一步是传奇「地板图 + 光照图 → 合成」的等价物：离屏之间的合成，不是离屏到画布。",
                        x, cy, new Color(150, 165, 195));
                    break;

                case Screen.Nested:
                    if (_rt1 is null) return;
                    Panel(batch, _rt1, x, BodyTop, "离屏1（嵌套模式下先画画布，再切它，再切回画布）");

                    float vy = NestedVerdictY;
                    for (int i = 0; i < 5; i++)
                        batch.Draw(KDefaultRes.DefaultTexture2D,
                            new Rectangle((int)(x + 380f + i * 44), (int)vy, 36, 36), new Color(70, 200, 120));

                    batch.DrawString(Font, "BEFORE 红（切 RT 前画在画布上）",
                        new Vector2(x, vy + 40f), new Color(255, 150, 150));
                    batch.DrawString(Font, "AFTER 绿（切回画布后画）",
                        new Vector2(x + 380f, vy + 40f), new Color(150, 255, 190));

                    float ny = vy + 68f;
                    ny = DrawLine(batch, "判定：红 BEFORE 与绿 AFTER 应同时可见；缺红＝切回画布时画布被清空",
                        x, ny, new Color(255, 206, 110));
                    DrawLine(batch, "本画面会自动把后台缓冲设为 PreserveContents（与传奇一致）；默认 DiscardContents 时切回画布本来就会清屏，属设计行为。",
                        x, ny, new Color(150, 165, 195));
                    break;

                case Screen.Accumulate:
                    if (_accum is null) return;
                    Panel(batch, _accum, x, BodyTop, "累积（首帧清一次，之后每帧加一条横杠）");
                    float ay = BodyTop + 260f;
                    ay = DrawLine(batch, "判定：黄杠 y 逐帧下移，25 帧内应铺满并保持稳定（＝PreserveContents 生效）",
                        x, ay, new Color(255, 206, 110));
                    ay = DrawLine(batch, "【关键】顶部那条红杠只在首帧画：红杠还在＝内容跨帧保留（load 生效）；",
                        x, ay, new Color(255, 150, 150));
                    ay = DrawLine(batch, "        红杠消失只剩黄杠＝每次切进 RT 都被清屏（load 未生效）；全黑＝RT 不可用",
                        x, ay, new Color(255, 150, 150));
                    DrawLine(batch, $"帧: {_frame}    本帧黄杠 y: {(_frame % BarsTotal) * (RtH / BarsTotal)}",
                        x, ay, new Color(150, 165, 195));
                    break;
            }
        }

        /// <summary>画一块面板（RT 内容 + 下方标题）。</summary>
        private void Panel(SpriteBatch batch, Texture2D texture, float x, float y, string caption)
        {
            batch.Draw(texture, new Rectangle((int)x, (int)y, RtW, RtH), Color.White);
            batch.DrawString(Font, caption, new Vector2(x, y + RtH + 6f), new Color(150, 165, 195));
        }
    }

}
