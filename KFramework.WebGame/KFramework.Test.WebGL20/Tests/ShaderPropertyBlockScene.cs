using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// <see cref="ShaderPropertyBlock"/>（每一次绘制的属性覆盖块）的用法与 DrawCall 对比：
    /// 同一张纹理、同一种逐精灵颜色差异，用 <b>3 种提交方式</b>各画 24 个精灵（按钮 / I 键切换），
    /// 读数给出实测 DrawCall 增量与期望值。
    /// <para>
    /// 关键机制（照 Unity）：
    /// <list type="bullet">
    ///   <item><description><b>不带块</b>：逐精灵差异走 <see cref="SpriteBatch.Draw"/> 的颜色参数 ——
    ///   它随顶点数据走、不参与分批键，整批合并成 1 次 DC。</description></item>
    ///   <item><description><b>带块</b>：块是「可变的 uniform」，引擎无法预知它之后会不会被改值，
    ///   所以每一笔带块的绘制都当场提交：24 个精灵 = 24 次 DC，<b>与值变不变无关</b>。
    ///   （Unity 里带 MaterialPropertyBlock 的物体同样不满足 SRP Batcher 的兼容条件，也会断合批。）</description></item>
    /// </list>
    /// 想让"逐精灵不同"仍然只一次 DrawCall，就得让差异随实例数据走 ——
    /// 那是另一条路子：<see cref="SpriteBatchGPUInstance"/>（第 7 页）；那条路上块值也能用，
    /// 但必须先用 <see cref="Material.SetInstanceChannels"/> 声明成实例通道（值编码进实例数据，不占 uniform）。
    /// </para>
    /// <para>交互：<b>按钮 / I 键</b>切换 3 种提交方式，画面完全一致、只有 DrawCall 不同。</para>
    /// </summary>
    public sealed class ShaderPropertyBlockScene : DemoScene
    {
        public override string Title => "8) ShaderPropertyBlock：覆盖 uniform 的用法与 DrawCall 对比";

        protected override string Description =>
            "同一张纹理 + 同一种逐精灵颜色差异，3 种提交方式各画 24 个精灵：画面完全一致，只有 DrawCall 增量不同。";

        /// <summary>每次提交画的精灵数（8 列 × 3 行）。</summary>
        private const int SpriteCount = 24;
        private const int SpriteCols = 8;

        /// <summary>块里放的属性名（默认 2D 程序不声明它 —— 同 Unity：设了没声明的属性不报错也不生效）。</summary>
        private const string BlockProperty = "uTint";

        /// <summary>一种提交方式。</summary>
        private enum SubmitMode
        {
            NoBlock,
            SharedBlock,
            PerSpriteBlock,
        }

        private const int ModeCount = 3;

        private static readonly string[] ModeNames =
        [
            "1) 不带块（差异走 Draw 的颜色）",
            "2) 共用块（整批只设一次值）",
            "3) 逐精灵改块值（每笔版本号都变）",
        ];

        private static readonly string[] ModePaths =
        [
            "差异随顶点数据走 → 不参与分批键",
            "同一个块、值整批不变",
            "同一个块、每笔 SetColor 一次",
        ];

        /// <summary>期望的 DrawCall 增量：不带块 1 次；带块逐笔提交。</summary>
        private static readonly long[] ExpectedDrawCalls =
        [
            1, SpriteCount, SpriteCount,
        ];

        private static readonly string[] ModeNotes =
        [
            "整批同纹理、同材质状态 → End 时合并成 1 次 drawElements。",
            "块只要出现就逐笔提交（块是可变 uniform，引擎无法预知它之后会不会被改值）→ 与值变不变无关。",
            "同上；每笔块版本号都变，分批键本来也会被打断。",
        ];

        private readonly Rectangle[] _modeRows = new Rectangle[ModeCount];

        private Texture2D? _chart;
        private SpriteFont? _small;
        private ShaderPropertyBlock? _block;
        private Material? _material;
        private Rectangle _button;
        private Rectangle _grid;
        private SubmitMode _mode = SubmitMode.NoBlock;
        private long _lastDrawCalls;

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
        }

        public override void Update()
        {
            Layout();
            UpdateMode();

            if (Input_Mouse.GetButtonDown(MouseButton.Left))
            {
                Vector2 p = Input_Mouse.Position;
                for (int m = 0; m < ModeCount; m++)
                {
                    if (_modeRows[m].Contains(p))
                    {
                        _mode = (SubmitMode)m;
                        return;
                    }
                }
            }
        }

        public override void Draw()
        {
            if (_chart is null || _small is null || _material is null || _block is null) return;

            Layout();

            // ---- 提交：当前方式画 24 个精灵，量 DrawCall 增量 ----
            long before = Device.Metrics.DrawCount;
            Submit(_mode);
            _lastDrawCalls = Device.Metrics.DrawCount - before;

            // ---- 文字 ----
            SpriteBatch batch = Batch;
            batch.Begin();
            DrawHeader(batch);
            DrawModeButton(batch);
            DrawModeList(batch);
            DrawReadout(batch);
            DrawConclusion(batch);
            DrawFooter(batch);
            batch.End();
        }

        /// <summary>按钮点击或 I 键：切到下一种提交方式。</summary>
        private void UpdateMode()
        {
            bool clicked = Input_Mouse.GetButtonDown(MouseButton.Left) && _button.Contains(Input_Mouse.Position);
            if (clicked || Input_KeyBoard.GetKeyDown(Keys.KeyI))
                _mode = (SubmitMode)(((int)_mode + 1) % ModeCount);
        }

        /// <summary>按当前方式提交 24 个精灵（逐精灵颜色不同，差异来源按方式决定）。</summary>
        private void Submit(SubmitMode mode)
        {
            Batch.Begin(_material!);

            // 共用块：整批只设一次值（版本号不变）；逐精灵模式则在循环里改值（版本号每笔都变）。
            if (mode == SubmitMode.SharedBlock)
            {
                _block!.Clear();
                _block.SetColor(BlockProperty, Color.White);
            }

            for (int i = 0; i < SpriteCount; i++)
            {
                Rectangle rect = SpriteRect(i);
                Color tint = Hue(i * 0.13f);   // 逐精灵色调：3 种方式的画面完全一致

                switch (mode)
                {
                    case SubmitMode.NoBlock:
                        // 差异随顶点数据走：不参与分批键 → 整批合并。
                        Batch.Draw(_chart!, rect, tint);
                        break;

                    case SubmitMode.SharedBlock:
                        Batch.Draw(_chart!, rect, tint, _block);
                        break;

                    default:
                        // 逐精灵改块值：每笔的块版本号都变。
                        _block!.Clear();
                        _block.SetColor(BlockProperty, tint);
                        Batch.Draw(_chart!, rect, tint, _block);
                        break;
                }
            }

            Batch.End();
        }

        /// <summary>按钮 / 方式列表 / 精灵网格的矩形（Update 与 Draw 共用，保证点击判定与画面一致）。</summary>
        private void Layout()
        {
            const float x0 = 28f;

            _button = new Rectangle(28, 74, 470, 30);

            float listTop = 112f;
            float rowHeight = _small!.LineSpacing + 4f;
            for (int m = 0; m < ModeCount; m++)
                _modeRows[m] = new Rectangle(28, (int)(listTop + m * rowHeight), 620, (int)rowHeight - 4);

            float gridTop = listTop + ModeCount * rowHeight + 8f;
            float gridBottom = Device.Viewport.Height - 186f;
            _grid = new Rectangle(28, (int)gridTop,
                                  (int)Math.Max(240f, Device.Viewport.Width - x0 * 2f),
                                  (int)Math.Max(60f, gridBottom - gridTop));
        }

        /// <summary>网格里第 <paramref name="index"/> 个精灵的矩形。</summary>
        private Rectangle SpriteRect(int index)
        {
            int cellWidth = Math.Max(6, _grid.Width / SpriteCols);
            int cellHeight = Math.Max(6, _grid.Height / (SpriteCount / SpriteCols));
            int size = Math.Max(4, Math.Min(cellWidth, cellHeight) - 6);

            int col = index % SpriteCols;
            int row = index / SpriteCols;
            return new Rectangle(_grid.X + col * cellWidth, _grid.Y + row * cellHeight, size, size);
        }

        private void DrawModeButton(SpriteBatch batch)
        {
            bool hover = _button.Contains(Input_Mouse.Position);
            batch.Draw(KDefaultRes.DefaultTexture2D, _button, hover ? new Color(58, 84, 130) : new Color(34, 46, 72));
            batch.DrawString(Font, $"当前方式：{ModeNames[(int)_mode]}    ← 点击按钮 / I 键切换",
                new Vector2(_button.X + 14f, _button.Y + 6f), Color.White);
        }

        /// <summary>3 种方式的对照表：高亮当前项，并给出期望 DC（点条目也能直接切过去）。</summary>
        private void DrawModeList(SpriteBatch batch)
        {
            SpriteFont font = _small!;
            for (int m = 0; m < ModeCount; m++)
            {
                Rectangle row = _modeRows[m];
                bool current = m == (int)_mode;

                if (current)
                    batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(row.X - 6, row.Y - 2, row.Width + 12, row.Height + 4),
                               new Color(34, 46, 72));

                batch.DrawString(font, $"{ModeNames[m]}    期望 DC = {ExpectedDrawCalls[m]}",
                    new Vector2(row.X, row.Y), current ? new Color(210, 230, 255) : new Color(140, 158, 190));
            }
        }

        private void DrawReadout(SpriteBatch batch)
        {
            SpriteFont font = _small!;
            float y = Device.Viewport.Height - 146f;

            Color color = _lastDrawCalls == ExpectedDrawCalls[(int)_mode] ? new Color(120, 200, 160) : new Color(255, 150, 150);

            batch.DrawString(font, $"实测：{SpriteCount} 个精灵 → DrawCall 增量 = {_lastDrawCalls}（期望 {ExpectedDrawCalls[(int)_mode]}）",
                new Vector2(28f, y), color);
            batch.DrawString(font, ModePaths[(int)_mode], new Vector2(28f, y + font.LineSpacing + 4f), new Color(150, 165, 195));
            batch.DrawString(font, ModeNotes[(int)_mode], new Vector2(28f, y + (font.LineSpacing + 4f) * 2f), new Color(150, 165, 195));
        }

        private void DrawConclusion(SpriteBatch batch)
        {
            SpriteFont font = _small!;
            float y = Device.Viewport.Height - 92f;
            float step = font.LineSpacing + 4f;

            batch.DrawString(font, "· 块的价值：多个物体共用同一个材质，却能各自覆盖不同的 shader 变量（照 Unity 的 renderer.SetPropertyBlock）。",
                new Vector2(28f, y), new Color(150, 165, 195));
            batch.DrawString(font, "· 块的代价：一次 draw 只带一份 uniform，所以带块的绘制当场提交 → 24 个精灵 = 24 次 DC（与值变不变无关）。",
                new Vector2(28f, y + step), new Color(255, 206, 110));
            batch.DrawString(font, "· 想「逐精灵不同 + 只 1 次 DC」请走另一条路：第 7 页的 SpriteBatchGPUInstance（属性用 SetInstanceChannels 声明成实例通道后，块值随实例数据走）。",
                new Vector2(28f, y + step * 2f), new Color(120, 200, 160));
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
