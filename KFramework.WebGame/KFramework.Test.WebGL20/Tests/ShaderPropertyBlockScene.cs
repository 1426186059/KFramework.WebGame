using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// <see cref="ShaderPropertyBlock"/>（每一次绘制的属性覆盖块）的用法与 DrawCall 对比：
    /// 同一张纹理、同一种逐精灵颜色差异，用 <b>6 种提交方式</b>各画 24 个精灵（按钮 / I 键切换），
    /// 读数给出实测 DrawCall 增量与期望值，用来一眼看出"块"对分批的影响。
    /// <para>
    /// 6 种方式 = 提交路径（逐顶点 / GPU 实例化）× 逐精灵差异来源（不带块 / 共用块 / 逐精灵改块值）：
    /// <list type="bullet">
    ///   <item><description><b>不带块</b>：差异走 <see cref="SpriteBatch.Draw"/> 的颜色参数 ——
    ///   它随顶点（或实例数据）走，不参与分批键，整批合并成 1 次 DC。</description></item>
    ///   <item><description><b>带块</b>：块能覆盖任意着色器属性（含纹理 / 矩阵），但它是「可变的 uniform」——
    ///   引擎无法预知它之后会不会被改值，所以<b>每一笔带块的绘制都当场提交</b>：24 个精灵 = 24 次 DC，
    ///   与"值有没有变"无关。</description></item>
    ///   <item><description><b>实例化 + 带块</b>：带块的绘制退回逐顶点路径，DC 与逐顶点一致。
    ///   这与 Unity 不同：Unity 能把"实例化属性"的值写进实例缓冲，所以实例化下用块仍然只 1 次 DC；
    ///   本引擎缺少"每实例属性通道"（`SpriteInstance` 里只有矩形/旋转/颜色/UV），故做不到。</description></item>
    /// </list>
    /// </para>
    /// <para>交互：<b>按钮 / I 键</b>切换 6 种提交方式，画面完全一致、只有 DrawCall 不同。</para>
    /// </summary>
    public sealed class ShaderPropertyBlockScene : DemoScene
    {
        public override string Title => "8) ShaderPropertyBlock：块的用法 + 实例化下的 DrawCall 对比";

        protected override string Description =>
            "同一张纹理 + 同一种逐精灵颜色差异，6 种提交方式各画 24 个精灵：画面完全一致，只有 DrawCall 增量不同。";

        /// <summary>每次提交画的精灵数（8 列 × 3 行）。</summary>
        private const int SpriteCount = 24;
        private const int SpriteCols = 8;

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
            "2) 逐顶点 + 共用块（值整批不变）",
            "3) 逐顶点 + 逐精灵改块值",
            "4) 实例化 + 无块",
            "5) 实例化 + 共用块（值整批不变）",
            "6) 实例化 + 逐精灵改块值",
        ];

        private static readonly string[] ModePaths =
        [
            "差异走 Draw 的颜色（随顶点数据走）",
            "同一个块、整批只设一次值",
            "同一个块、每笔改值（版本号每笔都变）",
            "差异走 Draw 的颜色（随实例缓冲走）",
            "同一个块、整批只设一次值",
            "同一个块、每笔改值（版本号每笔都变）",
        ];

        /// <summary>期望的 DrawCall 增量：不带块合并成 1 次；带块逐笔提交。</summary>
        private static readonly long[] ExpectedDrawCalls =
        [
            1, SpriteCount, SpriteCount,
            1, SpriteCount, SpriteCount,
        ];

        private static readonly string[] ModeNotes =
        [
            "整批同纹理、同材质状态 → End 时合并成 1 次 drawElements。",
            "块只要出现就逐笔提交（引擎无法预知它之后会不会被改值）→ 与值变不变无关。",
            "同上；每笔块版本号都变，分批键本来也会被打断。",
            "逐实例数据随实例缓冲走 → End 时 1 次 drawElementsInstanced。",
            "实例化模式下带块的绘制退回逐顶点路径（照 Unity 的\"非实例化属性把物体踢出实例化\"）。",
            "同上：开实例化不会让带块的绘制变快 —— 这点与 Unity 不同，见底部说明。",
        ];

        private readonly Rectangle[] _modeRows = new Rectangle[ModeCount];

        private Texture2D? _chart;
        private SpriteFont? _small;
        private ShaderPropertyBlock? _block;
        private Material? _material;
        private Rectangle _button;
        private Rectangle _grid;
        private SubmitMode _mode = SubmitMode.VertexNoBlock;
        private long _lastDrawCalls;
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
            bool supported = !IsInstanced(_mode) || _instancingSupported;
            _material.EnableInstancing = IsInstanced(_mode) && _instancingSupported;

            if (supported)
            {
                long before = Device.Metrics.DrawCount;
                Submit(_mode);
                _lastDrawCalls = Device.Metrics.DrawCount - before;
            }
            else
            {
                _lastDrawCalls = -1;
            }

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

            // 共用块：整批只设一次值（版本号不变）；逐精灵方式则在循环里改值（版本号每笔都变）。
            if (IsSharedBlock(mode))
            {
                _block!.Clear();
                _block.SetColor("uTint", Color.White);
            }

            for (int i = 0; i < SpriteCount; i++)
            {
                Rectangle rect = SpriteRect(i);
                Color tint = Hue(i * 0.13f);   // 逐精灵色调：6 种方式的画面完全一致

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

        /// <summary>按钮 / 方式列表 / 精灵网格的矩形（Update 与 Draw 共用，保证点击判定与画面一致）。</summary>
        private void Layout()
        {
            const float x0 = 28f;
            const float gap = 4f;

            _button = new Rectangle(28, 74, 470, 30);

            float listTop = 116f;
            float rowHeight = _small!.LineSpacing + 6f;
            for (int m = 0; m < ModeCount; m++)
                _modeRows[m] = new Rectangle(28, (int)(listTop + m * rowHeight), 430, (int)rowHeight - 6);

            float gridTop = listTop + ModeCount * rowHeight + 10f;
            float gridBottom = Device.Viewport.Height - 194f;
            _grid = new Rectangle(28, (int)gridTop,
                                  (int)Math.Max(240f, Device.Viewport.Width - x0 * 2f),
                                  (int)Math.Max(60f, gridBottom - gridTop - gap));
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

        /// <summary>6 种方式的对照表：高亮当前项，并给出期望 DC（点条目也能直接切过去）。</summary>
        private void DrawModeList(SpriteBatch batch)
        {
            SpriteFont font = _small!;
            for (int m = 0; m < ModeCount; m++)
            {
                Rectangle row = _modeRows[m];
                bool current = m == (int)_mode;

                if (current)
                    batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(row.X - 6, row.Y - 3, row.Width + 12, row.Height + 6),
                               new Color(34, 46, 72));

                batch.DrawString(font, $"{ModeNames[m]}    期望 DC = {ExpectedDrawCalls[m]}",
                    new Vector2(row.X, row.Y), current ? new Color(210, 230, 255) : new Color(140, 158, 190));
            }
        }

        private void DrawReadout(SpriteBatch batch)
        {
            SpriteFont font = _small!;
            float y = Device.Viewport.Height - 148f;

            string measured = _lastDrawCalls < 0 ? "当前后端不支持实例化" : $"{_lastDrawCalls}";
            Color color = _lastDrawCalls < 0 ? new Color(255, 206, 110)
                        : _lastDrawCalls == ExpectedDrawCalls[(int)_mode] ? new Color(120, 200, 160)
                        : new Color(255, 150, 150);

            batch.DrawString(font, $"实测：{SpriteCount} 个精灵 → DrawCall 增量 = {measured}（期望 {ExpectedDrawCalls[(int)_mode]}）",
                new Vector2(28f, y), color);
            batch.DrawString(font, ModePaths[(int)_mode], new Vector2(28f, y + font.LineSpacing + 4f), new Color(150, 165, 195));
            batch.DrawString(font, ModeNotes[(int)_mode], new Vector2(28f, y + (font.LineSpacing + 4f) * 2f), new Color(150, 165, 195));
        }

        private void DrawConclusion(SpriteBatch batch)
        {
            SpriteFont font = _small!;
            float y = Device.Viewport.Height - 92f;

            batch.DrawString(font, "· 不带块：逐精灵差异随顶点 / 实例数据走 → 整批合并；带块：块是可变的 uniform → 每笔当场提交。",
                new Vector2(28f, y), new Color(150, 165, 195));
            batch.DrawString(font, "· 与 Unity 的差异：Unity 能把「实例化属性」写进实例缓冲（实例化 + 块仍 1 次 DC）；本引擎没有每实例属性通道，故退回逐顶点。",
                new Vector2(28f, y + font.LineSpacing + 4f), new Color(255, 206, 110));
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
