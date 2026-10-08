using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// <see cref="ShaderPropertyBlock"/>（每一次绘制的属性覆盖块）的用法与 DrawCall 对比：
    /// 同一张纹理、同一种逐精灵颜色差异，用 <b>6 种提交方式</b>各画 24 个精灵（6 块面板同屏显示），
    /// 每块面板下方给出实测 DrawCall 增量与期望值，用来一眼看出"块"对分批的影响。
    /// <para>
    /// 6 种方式 = 提交路径（逐顶点 / GPU 实例化）× 逐精灵差异来源（不带块 / 共用块 / 逐精灵改块值）：
    /// <list type="bullet">
    ///   <item><description><b>不带块</b>：差异走 <see cref="SpriteBatch.Draw"/> 的颜色参数 ——
    ///   它随顶点（或实例数据）走，不参与分批键，整批合并成 1 次 DC。</description></item>
    ///   <item><description><b>带块</b>：块能覆盖任意着色器属性（含纹理 / 矩阵），但它是「可变的 uniform」——
    ///   引擎无法预知它之后会不会被改值，所以<b>每一笔带块的绘制都当场提交</b>：24 个精灵 = 24 次 DC，
    ///   与"值有没有变"无关（本页把"共用块"和"逐精灵改块值"两种都列了出来，DC 一样）。</description></item>
    ///   <item><description><b>实例化 + 带块</b>：带块的绘制退回逐顶点路径
    ///   （照 Unity：非实例化属性把物体踢出实例化），所以 DC 与逐顶点路径完全一致。</description></item>
    /// </list>
    /// 结论：要让"逐精灵不同"不切批，差异就得随顶点 / 实例数据走；块的价值是"能覆盖任意属性"，代价是逐笔一次 DC。
    /// </para>
    /// </summary>
    public sealed class ShaderPropertyBlockScene : DemoScene
    {
        public override string Title => "8) ShaderPropertyBlock：块的用法 + 实例化下的 DrawCall 对比";

        protected override string Description =>
            "同一张纹理 + 同一种逐精灵颜色差异，用 6 种提交方式各画 24 个精灵：画面完全一致，只有 DrawCall 增量不同。";

        /// <summary>每块面板画的精灵数（6 列 × 4 行）。</summary>
        private const int SpriteCount = 24;
        private const int SpriteCols = 6;

        /// <summary>面板排布：3 列 × 2 行。</summary>
        private const int PanelCols = 3;

        /// <summary>一种提交方式 = 提交路径（逐顶点 / 实例化）× 逐精灵差异来源（无块 / 共用块 / 逐精灵改块值）。</summary>
        private enum SubmitMode
        {
            VertexNoBlock,
            VertexSharedBlock,
            VertexPerSpriteBlock,
            InstancedNoBlock,
            InstancedSharedBlock,
            InstancedPerSpriteBlock,
        }

        private const int ModeCount = 6;

        private static readonly string[] ModeNames =
        [
            "1) 逐顶点 + 无块",
            "2) 逐顶点 + 共用块",
            "3) 逐顶点 + 逐精灵改块值",
            "4) 实例化 + 无块",
            "5) 实例化 + 共用块",
            "6) 实例化 + 逐精灵改块值",
        ];

        private static readonly string[] ModePaths =
        [
            "差异走 Draw 的颜色（随顶点）",
            "同一个块、值整批不变",
            "同一个块、每笔改值（版本号变）",
            "差异走 Draw 的颜色（随实例数据）",
            "同一个块、值整批不变",
            "同一个块、每笔改值（版本号变）",
        ];

        /// <summary>期望的 DrawCall 增量：不带块合并成 1 次；带块逐笔提交。</summary>
        private static readonly long[] ExpectedDrawCalls =
        [
            1, SpriteCount, SpriteCount,
            1, SpriteCount, SpriteCount,
        ];

        private readonly Rectangle[] _panels = new Rectangle[ModeCount];
        private readonly long[] _measured = new long[ModeCount];

        private Texture2D? _chart;
        private SpriteFont? _small;
        private ShaderPropertyBlock? _block;
        private Material? _material;
        private bool _instancingSupported = true;

        public override void LoadContent()
        {
            _chart = MakeChecker(64, Color.White, new Color(38, 48, 70));
            _small = new SpriteFont(Device, 13f);
            _block = new ShaderPropertyBlock();
            _material = new Material
            {
                Blend = BlendState.NonPremultiplied,
                Sampler = SamplerState.Point,
            };

            // 后端是否支持 GPU 实例化（WebGPU 尚未接入）：探测一次，避免整页在 Begin 时抛异常。
            var probe = new SpriteInstancer(Device, _chart);
            _instancingSupported = probe.IsSupported;
            probe.Dispose();
        }

        public override void Draw()
        {
            if (_chart is null || _small is null || _material is null || _block is null) return;

            UpdateLayout();

            // ---- 6 种提交方式各画一次，逐个量 DrawCall 增量（每块面板自己 Begin/End）----
            for (int m = 0; m < ModeCount; m++)
            {
                SubmitMode mode = (SubmitMode)m;
                if (IsInstanced(mode) && !_instancingSupported)
                {
                    _measured[m] = -1;
                    continue;
                }

                _material.EnableInstancing = IsInstanced(mode);
                long before = Device.Metrics.DrawCount;
                Submit(mode, _panels[m]);
                _measured[m] = Device.Metrics.DrawCount - before;
            }

            // ---- 文字（页眉 + 面板标签/读数 + 结论 + 页脚）----
            SpriteBatch batch = Batch;
            batch.Begin();
            DrawHeader(batch);

            for (int m = 0; m < ModeCount; m++) DrawPanelText(batch, m);
            DrawConclusion(batch);
            DrawFooter(batch);

            batch.End();
        }

        /// <summary>按当前模式提交一块面板：24 个精灵，逐精灵颜色不同，差异来源按模式决定。</summary>
        private void Submit(SubmitMode mode, Rectangle panel)
        {
            Batch.Begin(_material!);

            // 共用块：整批只设一次值（版本号不变）；逐精灵模式则在循环里改值（版本号每笔都变）。
            if (IsSharedBlock(mode))
            {
                _block!.Clear();
                _block.SetColor("uTint", Color.White);
            }

            for (int i = 0; i < SpriteCount; i++)
            {
                Rectangle rect = SpriteRect(panel, i);
                Color tint = Hue(i * 0.13f);   // 逐精灵色调：所有面板都一样，保证画面可对照

                switch (mode)
                {
                    case SubmitMode.VertexNoBlock:
                    case SubmitMode.InstancedNoBlock:
                        // 差异随顶点 / 实例数据走：不参与分批键 → 整批合并。
                        Batch.Draw(_chart!, rect, tint);
                        break;

                    case SubmitMode.VertexSharedBlock:
                    case SubmitMode.InstancedSharedBlock:
                        Batch.Draw(_chart!, rect, tint, _block);
                        break;

                    default:
                        // 逐精灵改块值：每笔的块版本号都变（引擎仍会逐笔提交）。
                        _block!.Clear();
                        _block.SetColor("uTint", tint);
                        Batch.Draw(_chart!, rect, tint, _block);
                        break;
                }
            }

            Batch.End();
        }

        private static bool IsInstanced(SubmitMode mode)
            => mode is SubmitMode.InstancedNoBlock or SubmitMode.InstancedSharedBlock or SubmitMode.InstancedPerSpriteBlock;

        private static bool IsSharedBlock(SubmitMode mode)
            => mode is SubmitMode.VertexSharedBlock or SubmitMode.InstancedSharedBlock;

        /// <summary>面板排布：3 列 × 2 行，底部留出结论与页脚所需高度。</summary>
        private void UpdateLayout()
        {
            const float x0 = 28f;
            const float y0 = 84f;
            const float gap = 14f;

            float availableWidth = Math.Max(240f, Device.Viewport.Width - x0 * 2f);
            float availableHeight = Math.Max(120f, Device.Viewport.Height - y0 - 152f);

            float panelWidth = (availableWidth - gap * (PanelCols - 1)) / PanelCols;
            float panelHeight = (availableHeight - gap) / 2f;

            for (int m = 0; m < ModeCount; m++)
            {
                int col = m % PanelCols;
                int row = m / PanelCols;
                _panels[m] = new Rectangle((int)(x0 + col * (panelWidth + gap)),
                                           (int)(y0 + row * (panelHeight + gap)),
                                           (int)panelWidth, (int)panelHeight);
            }
        }

        /// <summary>面板内第 <paramref name="index"/> 个精灵的矩形（上方留两行标签、下方留一行读数）。</summary>
        private static Rectangle SpriteRect(Rectangle panel, int index)
        {
            const int labelHeight = 36;
            const int readoutHeight = 18;

            int gridHeight = Math.Max(16, panel.Height - labelHeight - readoutHeight);
            int cellWidth = Math.Max(6, panel.Width / SpriteCols);
            int cellHeight = Math.Max(6, gridHeight / (SpriteCount / SpriteCols));
            int size = Math.Max(4, Math.Min(cellWidth, cellHeight) - 4);

            int col = index % SpriteCols;
            int row = index / SpriteCols;
            return new Rectangle(panel.X + 4 + col * cellWidth, panel.Y + labelHeight + row * cellHeight, size, size);
        }

        private void DrawPanelText(SpriteBatch batch, int index)
        {
            Rectangle panel = _panels[index];
            SpriteFont font = _small!;
            float x = panel.X + 4f;

            batch.DrawString(font, ModeNames[index], new Vector2(x, panel.Y + 2f), new Color(200, 220, 255));
            batch.DrawString(font, ModePaths[index],
                new Vector2(x, panel.Y + 2f + font.LineSpacing + 4f), new Color(140, 158, 190));

            string reading = _measured[index] < 0
                ? "当前后端不支持实例化"
                : $"实测 DC = {_measured[index]}（期望 {ExpectedDrawCalls[index]}）";
            Color color = _measured[index] < 0 ? new Color(255, 206, 110)
                        : _measured[index] == ExpectedDrawCalls[index] ? new Color(120, 200, 160)
                        : new Color(255, 150, 150);

            batch.DrawString(font, reading, new Vector2(x, panel.Y + panel.Height - 16f), color);
        }

        private void DrawConclusion(SpriteBatch batch)
        {
            SpriteFont font = _small!;
            float y = Device.Viewport.Height - 128f;

            batch.DrawString(font, "· 不带块：逐精灵差异随顶点 / 实例数据走 → 整批合并（1 次 DC）。",
                new Vector2(28f, y), new Color(150, 165, 195));
            batch.DrawString(font, "· 带块：块是可变的 uniform，引擎无法预知它之后会不会被改值 → 每笔当场提交，与值变不变无关。",
                new Vector2(28f, y + font.LineSpacing + 4f), new Color(150, 165, 195));
            batch.DrawString(font, "· 实例化 + 带块：退回逐顶点路径（照 Unity：非实例化属性把物体踢出实例化）→ DC 与逐顶点一致。",
                new Vector2(28f, y + (font.LineSpacing + 4f) * 2f), new Color(150, 165, 195));
            batch.DrawString(font, "（块里设的 uTint 对默认 2D 程序是无关属性 —— 同 Unity：设了没声明的属性不报错也不生效，故 6 块面板画面一致。）",
                new Vector2(28f, y + (font.LineSpacing + 4f) * 3f), new Color(120, 135, 165));
        }

        /// <summary>HSV → RGB（h 取 0~1）：给每个精灵一个明显不同的色调。</summary>
        private static Color Hue(float h)
        {
            h -= MathF.Floor(h);
            float s = 0.75f, v = 1f;
            float i = MathF.Floor(h * 6f);
            float f = h * 6f - i;
            float p = v * (1f - s);
            float q = v * (1f - f * s);
            float t = v * (1f - (1f - f) * s);

            return ((int)i % 6) switch
            {
                0 => new Color(v, t, p),
                1 => new Color(q, v, p),
                2 => new Color(p, v, t),
                3 => new Color(p, q, v),
                4 => new Color(t, p, v),
                _ => new Color(v, p, q),
            };
        }
    }
}
