using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>
    /// 渲染目标（RenderTarget2D）专项：顶部按钮<b>切换显示画面</b>，一次只显示一个画面。
    /// 画面划分与判据照 WebGPU 工程的 OffscreenScene（同一套测法，便于跨后端直接对照）。
    /// <list type="bullet">
    ///   <item><description><b>测试离屏</b>：内容画进离屏1，再把它当纹理贴到屏幕（离屏基本通路）。</description></item>
    ///   <item><description><b>测试无 MSAA</b>：MSAA=0 与 =4 并排。Canvas2D 的离屏就是一块 canvas，
    ///   根本没有"渲染目标级多重采样"，<c>preferredMultiSampleCount</c> 被忽略 —— 两张应<b>完全一样</b>，
    ///   一样才说明后端没有偷偷改变行为（本页是负向验证）。</description></item>
    ///   <item><description><b>测试RT→RT合成</b>：离屏1 铺满 + 离屏2 叠右下，合成进离屏3（离屏之间的合成）。</description></item>
    ///   <item><description><b>测试纹理合成A-F</b>：A 底 / B 红块 / C 球阵 / D 绿块 /
    ///   E = B+C+D / F = A+E 六张纹理按顺序铺开。E 的构造中途要【切走再切回】，故 E 即是判据。</description></item>
    ///   <item><description><b>测试画布来回切</b>：把目标换成<b>画布</b>，做 3 轮「切去离屏 → 切回画布」，
    ///   每轮回画布补画一个编号方块。画布即屏幕，结果直接可见。</description></item>
    ///   <item><description><b>测试累积</b>：只在首帧清一次，之后每帧加一条横杠，
    ///   验证 <c>PreserveContents</c> 是否真的保留内容。</description></item>
    /// </list>
    /// <para>
    /// 与 WebGL / WebGPU 的关键差异：Canvas2D 的渲染目标是<b>另一块离屏 canvas</b> ——
    /// 同一个 id 同时登记进"目标表"与"纹理表"，既能被画进去（SetRenderTarget），
    /// 也能当普通纹理被 <c>drawImage</c> 采样（上屏 / 当中间图），没有 FBO / 附件 / resolve 那一套。
    /// </para>
    /// </summary>
    public sealed class OffscreenScene : DemoScene
    {
        public override string Title => "5) 渲染目标专项：离屏 / 无MSAA / 合成 / 嵌套 / 累积";

        protected override string Description
            => "点上方按钮切换显示画面（Esc 返回总纲）";

        /// <summary>显示画面。</summary>
        private enum Screen
        {
            Offscreen,
            MsaaIgnored,
            Composite,
            Pipeline,
            CanvasRoundTrip,
            Accumulate,
        }

        private static readonly (string Name, Screen Value)[] Screens =
        [
            ("测试离屏", Screen.Offscreen),
            ("测试无MSAA", Screen.MsaaIgnored),
            ("测试RT→RT合成", Screen.Composite),
            ("测试纹理合成A-F", Screen.Pipeline),
            ("测试画布来回切", Screen.CanvasRoundTrip),
            ("测试累积", Screen.Accumulate),
        ];

        private const int RtW = 320;
        private const int RtH = 200;

        private const int BtnH = 30;
        private const int BtnGap = 8;
        private const int BarsTotal = 25;

        /// <summary>
        /// 正文区顶边 = 最后一行按钮的底边 + 间距。做成动态是为了让按钮能自由换行
        /// （按钮数量随测试项增加，写死 118 会在按钮占两行时与正文重叠）。
        /// </summary>
        private float BodyTop => _buttons.Count > 0 ? _buttons[^1].Bottom + 20f : 118f;

        // 流水线式对照区：3 列 × 2 行（A B C / D E F）。
        // 间距要给标题留位置（标题从面板左端起，宽度 = PanelW + PanelGap），故 gap 取 30：
        // 3*290 + 2*30 + 56 = 986，在最窄的画布（约 1032）内仍有余量。
        private const int PanelW = 290;
        private const int PanelH = 181;     // 保持 320:200 的比例（1.6:1）
        private const int PanelGap = 30;

        // 「测试画布来回切」：三组并排，逐层剥离变量 —— 哪一组开始缺，问题就出在哪一层。
        //   ① 一个批次画完 6 个：既不切换、也不分批（最干净的基准）
        //   ② 分多个批次画完：不切换，只比 ① 多了"批次"
        //   ③ 分多个批次 + 每轮切走再切回：再比 ② 多了"切换"
        private const float CellW = 110f;
        private const float CellH = 110f;
        private const float CellGap = 18f;
        private const float RowGap = 44f;      // 同组内上下两排的间距
        private const float GroupPad = 14f;    // 组外框内边距
        private const float GroupGap = 40f;    // 两组之间的空白

        private const float GroupW = CellW * 3 + CellGap * 2;
        private const float GroupBoxW = GroupW + GroupPad * 2;
        private const float GroupBoxH = CellH * 2 + RowGap + GroupPad * 2;

        private const float OneX = 28f;                            // ① 单批基准
        private const float ManyX = OneX + GroupBoxW + GroupGap;   // ② 分批不切换
        private const float SwapX = ManyX + GroupBoxW + GroupGap;  // ③ 分批 + 切换

        // 上排顶边比正文区再低 42：给上方那一行【组名】留位置，否则组名会压到按钮区里。
        private float TripBaseY => BodyTop + 42f;
        private float TripY => TripBaseY + CellH + RowGap;
        private static float CellX(float groupX, int i) => groupX + i * (CellW + CellGap);

        private Screen _screen = Screen.Offscreen;
        private readonly List<Rectangle> _buttons = [];

        private RenderTarget2D? _rt1;      // 离屏1（无 MSAA）
        private RenderTarget2D? _rt2;      // 离屏2（无 MSAA）
        private RenderTarget2D? _rtMsaa;   // 请求 MSAA=4（Canvas2D 忽略）
        private RenderTarget2D? _rtComp;   // 合成目标
        // 流水线：A 起始底 → B/C/D 三段叠加 → E = B+C+D 合成 → F = A + E
        private RenderTarget2D? _rtA;      // A 纹理：起始底（不透明）
        private RenderTarget2D? _rtB;      // B 纹理：红块（透明底）
        private RenderTarget2D? _rtC;      // C 纹理：球阵（透明底，中途要切去画的那张）
        private RenderTarget2D? _rtD;      // D 纹理：绿块（透明底）
        private RenderTarget2D? _rtE;      // E 纹理：合成纹理 = B + C + D（中途要切走再切回）
        private RenderTarget2D? _rtF;      // F 纹理：最终 = A + E
        private RenderTarget2D? _accum;    // 累积

        // 首次 RenderOffscreen 时创建（离屏阶段早于任何 Draw），故声明为非空。
        private Texture2D _ball = null!;      // 带高光的球：曲线边缘，看走样最直观
        private Texture2D _bgCool = null!;    // 冷色渐变底
        private Texture2D _bgWarm = null!;    // 暖色渐变底
        private int _frame;
        private bool _accumSeeded;

        public override void Update()
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

            // 横排放不下就换行（按钮数量会随测试项增加，写死单行会溢出窄画布）。
            // BodyTop 会跟着最后一行的底部走，所以下方布局无需关心按钮占了几行。
            float x = 28f;
            float y = 76f;
            float limit = Math.Max(240f, Device.Viewport.Width - 28f);

            for (int i = 0; i < Screens.Length; i++)
            {
                int w = 28 + EstimateWidth(Screens[i].Name);
                if (x + w > limit && x > 28f)
                {
                    x = 28f;
                    y += BtnH + 6f;
                }
                _buttons.Add(new Rectangle((int)x, (int)y, w, BtnH));
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

                    // 只有以画布为目标的测试才需要保住画布上已画的内容
                    //（PresentationParameters 默认是 DiscardContents，切回画布本来就会清屏，属设计行为）。
                    Device.PresentationParameters.RenderTargetUsage = _screen == Screen.CanvasRoundTrip
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

                case Screen.MsaaIgnored:
                    _rt1 ??= NewTarget(0);
                    _rtMsaa ??= NewTarget(4);
                    // 注意：不用球阵 —— 球的圆边是【纹理 alpha】形成的，任何 MSAA 都管不到它。
                    // 这里画旋转的实心色块（真正的几何边缘），好让两张的"逐像素一致"更容易判读。
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

                case Screen.Pipeline:
                    // A~F 全是纹理（画布不参与编号，它是另一个独立目标，单独一个画面测）。
                    _rtA ??= NewTarget(0);
                    _rtB ??= NewTarget(0);
                    _rtC ??= NewTarget(0);
                    _rtD ??= NewTarget(0);
                    _rtE ??= NewTarget(0, RenderTargetUsage.PreserveContents);
                    _rtF ??= NewTarget(0);

                    // —— A / B / C / D：四张基础纹理，各自单独画出来 ——
                    RenderStageAlone(_rtA, batch, 0);           // A：底（不透明）
                    RenderStageTransparent(_rtB, batch, 1);     // B：红块（透明底）
                    RenderStageTransparent(_rtC, batch, 2);     // C：球阵（透明底）
                    RenderStageTransparent(_rtD, batch, 3);     // D：绿块（透明底）

                    // —— E = B + C + D ——
                    // 关键：这一步【中途要切走再切回】，正是对"跨通道"的测试。
                    //   ① 绑定 E，画 B 红块
                    //   ② 切去 C（另一张纹理）画球阵
                    //   ③ 切回 E，把 C 贴上去，再画 D 绿块
                    // 判据：E 应当同时有 红块 + 球阵 + 绿块；
                    //       若"切回 E"时内容被清掉，红块（①画的）就会消失。
                    Device.SetRenderTarget(_rtE);
                    Device.Clear(new Color(0, 0, 0, 0));
                    batch.Begin();
                    DrawStage(batch, 1, 0, 0, RtW, RtH);        // ① 画 B 红块
                    batch.End();

                    Device.SetRenderTarget(_rtC);               // ② 切去 C（跨通道）
                    Device.Clear(new Color(0, 0, 0, 0));
                    batch.Begin();
                    DrawStage(batch, 2, 0, 0, RtW, RtH);
                    batch.End();

                    Device.SetRenderTarget(_rtE);               // ③ 切回 E，接着画
                    batch.Begin();
                    batch.Draw(_rtC, new Rectangle(0, 0, RtW, RtH), null, Color.White);
                    DrawStage(batch, 3, 0, 0, RtW, RtH);        // 画 D 绿块
                    batch.End();
                    Device.SetRenderTarget(null);

                    // —— F = A + E：把底与合成结果叠到一起（不涉及跨通道）——
                    Device.SetRenderTarget(_rtF);
                    Device.Clear(new Color(0, 0, 0, 0));
                    batch.Begin();
                    DrawStage(batch, 0, 0, 0, RtW, RtH);        // 画 A 底
                    batch.Draw(_rtE, new Rectangle(0, 0, RtW, RtH), null, Color.White);
                    batch.End();
                    Device.SetRenderTarget(null);
                    break;

                case Screen.CanvasRoundTrip:
                    // 画布来回切：内容直接画在画布（屏幕）上，不做成面板。
                    // 三组并排、逐层加变量，故绘制顺序必须【由最脏到最干净】：
                    // 会切换目标的 ③ 先画，否则它造成的清屏会连带擦掉后画的组，对照就失真了。
                    _rtC ??= NewTarget(0);

                    // —— ③ 分批 + 每轮切走再切回（变量最多）——
                    batch.Begin();
                    for (int i = 0; i < 3; i++)
                        DrawCell(batch, i, CellX(SwapX, i), TripBaseY);
                    batch.End();

                    for (int i = 0; i < 3; i++)
                    {
                        Device.SetRenderTarget(_rtC);           // 切去离屏（跨通道）
                        Device.Clear(new Color(0, 0, 0, 0));
                        batch.Begin();
                        DrawStage(batch, 2, 0, 0, RtW, RtH);    // 离屏上画什么不重要，只为制造"切走"
                        batch.End();

                        Device.SetRenderTarget(null);           // 切回画布
                        batch.Begin();
                        DrawCell(batch, i, CellX(SwapX, i), TripY);
                        batch.End();
                    }

                    // —— ② 分批，但从不切换目标（只比 ① 多了"批次"）——
                    batch.Begin();
                    for (int i = 0; i < 3; i++)
                        DrawCell(batch, i, CellX(ManyX, i), TripBaseY);
                    batch.End();

                    for (int i = 0; i < 3; i++)
                    {
                        batch.Begin();
                        DrawCell(batch, i, CellX(ManyX, i), TripY);
                        batch.End();
                    }

                    // —— ① 一个批次画完 6 个（既不切换也不分批，最干净的基准）——
                    batch.Begin();
                    for (int i = 0; i < 3; i++)
                    {
                        DrawCell(batch, i, CellX(OneX, i), TripBaseY);
                        DrawCell(batch, i, CellX(OneX, i), TripY);
                    }
                    batch.End();

                    // —— 最后统一画槽位框与组外框：内容即便被抹掉，也看得见"缺了哪几个"——
                    batch.Begin();
                    for (int i = 0; i < 3; i++)
                    {
                        DrawSlot(batch, i, CellX(OneX, i), TripBaseY);
                        DrawSlot(batch, i, CellX(OneX, i), TripY);
                        DrawSlot(batch, i, CellX(ManyX, i), TripBaseY);
                        DrawSlot(batch, i, CellX(ManyX, i), TripY);
                        DrawSlot(batch, i, CellX(SwapX, i), TripBaseY);
                        DrawSlot(batch, i, CellX(SwapX, i), TripY);
                    }

                    // 三组各一个大边框，组界一眼分明
                    Frame(batch, OneX - GroupPad, TripBaseY - GroupPad,
                        GroupBoxW, GroupBoxH, new Color(126, 200, 255));
                    Frame(batch, ManyX - GroupPad, TripBaseY - GroupPad,
                        GroupBoxW, GroupBoxH, new Color(150, 220, 170));
                    Frame(batch, SwapX - GroupPad, TripBaseY - GroupPad,
                        GroupBoxW, GroupBoxH, new Color(255, 206, 110));
                    batch.End();
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
            batch.Draw(kind == 0 ? _bgCool : _bgWarm, new Rectangle(0, 0, RtW, RtH), null, Color.White);

            // ② 彩色球阵：色相随时间流动 + 轻微呼吸缩放，画面不至于呆板
            float originX = (RtW - (cols - 1) * cell) * 0.5f;
            float originY = (RtH - (rows - 1) * cell) * 0.5f;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float hue = (c * 34f + r * 22f + _frame * 0.5f) % 360f;
                    float pulse = 1f + 0.07f * MathF.Sin((_frame + c * 4 + r * 6) * 0.05f);
                    batch.DrawCentered(_ball,
                        new Vector2(originX + c * cell, originY + r * cell),
                        Hsv(hue, 0.55f, 1f),
                        0f, ballScale * pulse);
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
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(2, 2, m, m), null, new Color(220, 70, 70));
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(w - m - 2, 2, m, m), null, new Color(70, 200, 120));
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(2, h - m - 2, m, m), null, new Color(70, 140, 220));
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(w - m - 2, h - m - 2, m, m), null, new Color(230, 200, 80));
        }

        /// <summary>
        /// 供"无 MSAA"画面用的内容：全部由<b>旋转的实心色块</b>构成。
        /// <para>
        /// 关键：三角形（几何）边缘才是 MSAA 的作用对象；球体那种靠纹理 alpha 形成的圆边不在其列。
        /// Canvas2D 后端没有 RT 级 MSAA，故这里画同样的内容进"请求 0x"与"请求 4x"两张 RT，
        /// 只要两张逐像素一致，就说明后端确实忽略了该请求（而不是悄悄改了行为）。
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
                batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle((int)cx, (int)cy, (int)len, 9), null,
                    Hsv((i * 360f / bars + spin * 40f) % 360f, 0.85f, 1f), a,
                    new Vector2(0.5f, 0.5f), null, SpriteEffects.None, 0f);
            }

            // 几个旋转方块，强化边缘锯齿观感
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * MathF.PI + spin * 0.7f;
                float s = 44f + i * 18f;
                batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle((int)cx, (int)cy, (int)s, (int)s), null,
                    Hsv((i * 70f) % 360f, 0.6f, 1f), a,
                    new Vector2(0.5f, 0.5f), null, SpriteEffects.None, 0f);
            }
        }

        /// <summary>把离屏1 与离屏2 一起画进合成目标（离屏之间的合成）。</summary>
        private void DrawComposite(SpriteBatch batch)
        {
            if (_rtComp is null || _rt1 is null || _rt2 is null) return;

            Device.SetRenderTarget(_rtComp);
            Device.Clear(new Color(8, 10, 16));

            batch.Begin();
            batch.Draw(_rt1, new Rectangle(0, 0, RtW, RtH), null, Color.White);                       // 离屏1 铺满
            batch.Draw(_rt2, new Rectangle(RtW / 2, RtH / 2, RtW / 2, RtH / 2), null, Color.White);   // 离屏2 叠在右下
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
            //（PreserveContents 生效），"铺不满"就得往别处查；若它消失、只剩黄杠，说明每次切进 RT 都被清屏。
            if (seed)
                batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(0, 0, RtW, 6), null, new Color(220, 70, 70));

            // 每帧画一条黄杠，y 逐帧下移（(帧 % 25) * 8），保留得住就会 25 帧内铺满。
            batch.Draw(KDefaultRes.DefaultTexture2D,
                new Rectangle(0, (_frame % BarsTotal) * barH, RtW, barH - 1), null, new Color(255, 200, 90));
            batch.End();

            Device.SetRenderTarget(null);
        }

        /// <summary>对照区的 x（0..2）。</summary>
        private static float PanelX(int index) => 28f + index * (PanelW + PanelGap);

        /// <summary>第二行的 y（D / E / F 所在行）。</summary>
        private float Row2Y => BodyTop + PanelH + 36f;

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
                case 0:     // A：底（不透明）
                    batch.Draw(_bgCool, new Rectangle((int)x, (int)y, (int)w, (int)h), null, Color.White);
                    break;

                case 1:     // B：红块，贴顶部
                    for (int i = 0; i < 4; i++)
                        batch.Draw(KDefaultRes.DefaultTexture2D,
                            new Rectangle((int)(x + 8 + i * 30), (int)(y + 3), 18, 18), null, new Color(220, 70, 70));
                    break;

                case 2:     // C：球阵
                    for (int r = 0; r < 2; r++)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            float hue = (c * 45f + r * 30f + _frame * 0.5f) % 360f;
                            batch.DrawCentered(_ball,
                                new Vector2(x + w * 0.24f + c * w * 0.19f, y + h * 0.46f + r * h * 0.22f),
                                Hsv(hue, 0.55f, 1f), 0f, 0.62f);
                        }
                    }
                    break;

                case 3:     // D：绿块，贴底部（与红块分居上下，一眼看出少了哪一段）
                    for (int i = 0; i < 4; i++)
                        batch.Draw(KDefaultRes.DefaultTexture2D,
                            new Rectangle((int)(x + 8 + i * 30), (int)(y + h - 21), 18, 18), null, new Color(70, 200, 120));
                    break;
            }
        }

        // ================================================================
        // 屏幕绘制
        // ================================================================

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            // 顶部按钮：当前画面高亮
            for (int i = 0; i < _buttons.Count; i++)
            {
                Rectangle rect = _buttons[i];
                bool current = Screens[i].Value == _screen;
                bool hover = rect.Contains(Input_Mouse.Position);

                batch.Draw(KDefaultRes.DefaultTexture2D, rect, null,
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
                    DrawLine(batch, "（高光跑到左下、或四角标记上下颠倒 → 说明离屏的投影 / 翻转取错了。Canvas2D 离屏与画布同构，应当完全一致）",
                        x, y, new Color(150, 165, 195));
                    break;

                case Screen.MsaaIgnored:
                    if (_rt1 is null || _rtMsaa is null) return;
                    Panel(batch, _rt1, x, BodyTop, "请求 MSAA=0");
                    Panel(batch, _rtMsaa, x + RtW + 48f, BodyTop, "请求 MSAA=4（Canvas2D 忽略）");
                    float my = BodyTop + 260f;
                    my = DrawLine(batch, "判定：两张应当【完全一样】—— Canvas2D 的离屏就是一块 canvas，没有多重采样，请求被忽略",
                        x, my, new Color(255, 206, 110));
                    my = DrawLine(batch, "若两张不同，说明后端没有如实忽略该请求（本页是负向验证）", x, my, new Color(255, 150, 150));
                    my = DrawLine(batch, "Canvas2D 的抗锯齿只来自浏览器对 drawImage 的合成，与 D3D / GL 的 MSAA 无关；",
                        x, my, new Color(150, 165, 195));
                    my = DrawLine(batch, "对几何边缘而言，旋转色块比球阵更能体现抗锯齿差异，故本画面不用球阵。",
                        x, my, new Color(150, 165, 195));
                    my = DrawLine(batch, "【对照】WebGL / WebGPU：离屏 MSAA 是渲染目标级别的（多重采样附件 + resolve），与画布 antialias 无关；",
                        x, my, new Color(150, 165, 195));
                    DrawLine(batch, "        Canvas2D：没有附件体系，MultiSampleCount 只记一条日志后忽略。",
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

                case Screen.Pipeline:
                    if (_rtA is not { } rtA || _rtB is not { } rtB || _rtC is not { } rtC
                        || _rtD is not { } rtD || _rtE is not { } rtE || _rtF is not { } rtF) return;

                    // 第一行：A → B → C
                    Panel(batch, rtA, PanelX(0), BodyTop, "A 底");
                    Panel(batch, rtB, PanelX(1), BodyTop, "B 红块");
                    Panel(batch, rtC, PanelX(2), BodyTop, "C 球阵");

                    // 第二行：D → E → F（E 是本页的判据：它经过"切走再切回"）
                    Panel(batch, rtD, PanelX(0), Row2Y, "D 绿块");
                    Panel(batch, rtE, PanelX(1), Row2Y, "E=B+C+D(含切换)");
                    Panel(batch, rtF, PanelX(2), Row2Y, "F=A+E");

                    float py = Row2Y + PanelH + 34f;
                    py = DrawLine(batch, "流水线：A 底 → B 红块 → C 球阵 → D 绿块 → E = B+C+D → F = A+E（全是纹理）",
                        x, py, new Color(255, 206, 110));
                    py = DrawLine(batch, "E 的构造经过【切走再切回】：画 B 红块 → 切去 C 画球阵 → 切回 E 贴上 C 并画 D 绿块",
                        x, py, new Color(255, 206, 110));
                    py = DrawLine(batch, "判据：E 应同时含 红块 + 球阵 + 绿块。若红块缺失 → 切回 E 时内容被清掉了",
                        x, py, new Color(255, 150, 150));
                    DrawLine(batch, "F = A + E 只是把底叠上去，不涉切换；F 缺什么取决于 E 缺什么",
                        x, py, new Color(150, 165, 195));
                    break;

                case Screen.CanvasRoundTrip:
                    // 组名单独占正文区第一行（BodyTop 那行），位于大边框上方且不压按钮
                    batch.DrawString(Font, "① 一批画完（基准）",
                        new Vector2(OneX, BodyTop), new Color(126, 200, 255));
                    batch.DrawString(Font, "② 分批，不切目标",
                        new Vector2(ManyX, BodyTop), new Color(150, 220, 170));
                    batch.DrawString(Font, "③ 分批 + 切目标",
                        new Vector2(SwapX, BodyTop), new Color(255, 206, 110));

                    float rty = TripY + CellH + 30f;
                    rty = DrawLine(batch, "三组各 6 个槽：① 最干净作基准，② 只多【批次】，③ 再多【切换】",
                        x, rty, new Color(255, 206, 110));
                    rty = DrawLine(batch, "哪组开始缺 → 问题就在那一层：①缺=绘制本身；②缺=批次清屏；③缺=切换清屏",
                        x, rty, new Color(255, 150, 150));
                    DrawLine(batch, $"后台缓冲策略={Device.PresentationParameters.RenderTargetUsage}"
                        + $"    视口 {Device.Viewport.Width}x{Device.Viewport.Height}",
                        x, rty, new Color(150, 165, 195));
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
            batch.Draw(texture, new Rectangle((int)x, (int)y, PanelW, PanelH), null, Color.White);
            batch.DrawString(Font, caption, new Vector2(x, y + PanelH + 6f), new Color(150, 165, 195));
        }

        /// <summary>画一个编号方块的<b>内容</b>（实心色块，不含边框）。</summary>
        private void DrawCell(SpriteBatch batch, int index, float x, float y)
        {
            batch.Draw(KDefaultRes.DefaultTexture2D,
                new Rectangle((int)x, (int)y, (int)CellW, (int)CellH), null, CellColor(index));
        }

        /// <summary>
        /// 画<b>槽位</b>（边框 + 编号）。
        /// <para>
        /// 必须在所有内容绘制【之后】统一画一遍：内容一旦被清屏抹掉，剩下的是一片空白，
        /// 光看空白无从判断"那里本该有什么"。槽位框留着，缺哪几个一眼就能数出来。
        /// </para>
        /// </summary>
        private void DrawSlot(SpriteBatch batch, int index, float x, float y)
        {
            Frame(batch, x, y, CellW, CellH, new Color(205, 218, 240));
            batch.DrawString(Font, (index + 1).ToString(),
                new Vector2(x + 8f, y + 6f), new Color(255, 255, 255));
        }

        private static Color CellColor(int index) => index switch
        {
            0 => new Color(200, 80, 80),
            1 => new Color(70, 190, 110),
            _ => new Color(90, 140, 220),
        };

        /// <summary>给一块区域描一圈 2px 边框（用来标出"这块是直接画在屏幕上的"）。</summary>
        private void Frame(SpriteBatch batch, float x, float y, float w, float h, Color color)
        {
            int ix = (int)x, iy = (int)y, iw = (int)w, ih = (int)h, t = 2;
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy, iw, t), null, color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy + ih - t, iw, t), null, color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy, t, ih), null, color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix + iw - t, iy, t, ih), null, color);
        }
    }

}
