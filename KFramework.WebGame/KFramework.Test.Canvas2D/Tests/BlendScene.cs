using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>
    /// 混合模式对照：用<b>引擎自带的 6 个</b> <see cref="BlendState"/> 画同一份内容，看 Canvas2D 的
    /// <c>globalCompositeOperation</c> 映射结果对不对。
    /// <para>
    /// Canvas2D 只有一个合成开关（没有逐绘制的 src / dst 因子），所以后端按"源 / 目标因子"的组合归类：
    /// 禁用混合与其余 → <c>source-over</c>；<c>SrcAlpha+One</c>、<c>One+One</c> → <c>lighter</c>；
    /// <c>DstColor+Zero</c>、<c>Zero+SrcColor</c> → <c>multiply</c>。
    /// </para>
    /// <para>
    /// 这批状态不是随便挑的：<see cref="BlendState.Additive"/>（发光）与 <see cref="BlendState.Multiply"/>
    /// （把光照图乘回主画面）正是传奇客户端唯一用到的两种"特殊"混合，所以本页同时也就是
    /// "传奇那几路混合能不能跑"的对照页。
    /// </para>
    /// <para>
    /// 本页自己管 Begin / End：混合状态挂在批次上，要逐格对照就必须一格一批。
    /// </para>
    /// </summary>
    public sealed class BlendScene : DemoScene
    {
        public override string Title => "4) 混合模式（引擎 6 个 BlendState 对照 globalCompositeOperation）";

        protected override string Description
            => "同一份内容分别用 6 个 BlendState 画一遍：看 Canvas2D 归到 source-over / lighter / multiply 的哪一类";

        private Texture2D? _tex;
        private int _frame;

        public override void Update() => _frame++;

        public override void Draw()
        {
            _tex ??= MakeChecker(64, new Color(232, 226, 214), new Color(52, 60, 84));
            Texture2D tex = _tex;

            SpriteBatch batch = Batch;

            // 版面：3 列 × 2 行。每格上方两行标签，格子 210 高。
            //   行1 标签 92/120，格子 150..360；行2 标签 378/406，格子 436..646；说明 652..
            float cellW = Math.Clamp((Device.Viewport.Width - 56f - 48f) / 3f, 220f, 340f);
            const float cellH = 210f;
            const float gapX = 24f;
            const float x0 = 28f;

            (BlendState Blend, string Label, string Comment)[] cells =
            [
                (BlendState.NonPremultiplied, "① NonPremultiplied", "→ source-over（默认）"),
                (BlendState.Additive,         "② Additive",         "→ lighter（SrcAlpha+One）"),
                (BlendState.AdditiveFull,     "③ AdditiveFull",     "→ lighter（One+One）"),
                (BlendState.Multiply,         "④ Multiply",         "→ multiply（光照压暗）"),
                (BlendState.AlphaBlend,       "⑤ AlphaBlend",       "→ source-over（预乘）"),
                (BlendState.Opaque,           "⑥ Opaque",           "→ source-over（关混合）"),
            ];

            // 第一遍：标签 + 格子外框（普通混合批次，保证框不被各格的混合状态影响）
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            DrawHeader(batch);

            for (int i = 0; i < cells.Length; i++)
            {
                (float x, float y) = CellAt(i, cellW, gapX, x0);
                DrawLine(batch, cells[i].Label, x, y - 58f, new Color(210, 230, 255));
                DrawLine(batch, cells[i].Comment, x, y - 30f, new Color(255, 206, 110));
                batch.Draw(KDefaultRes.DefaultTexture2D,
                    new Rectangle((int)x - 2, (int)y - 2, (int)cellW + 4, (int)cellH + 4), null, new Color(58, 70, 96));
            }

            batch.End();

            // 第二遍：逐格用各自的 BlendState 画同一份内容
            for (int i = 0; i < cells.Length; i++)
            {
                (float x, float y) = CellAt(i, cellW, gapX, x0);

                batch.Begin(SpriteSortMode.Deferred, cells[i].Blend, SamplerState.PointClamp);
                DrawCellContent(batch, tex, x, y, cellW, cellH);
                batch.End();
            }

            // 第三遍：页脚说明
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            DrawNotes(batch, 652f);
            DrawFooter(batch);
            batch.End();
        }

        private static (float X, float Y) CellAt(int index, float cellW, float gapX, float x0)
        {
            float x = x0 + (index % 3) * (cellW + gapX);
            float y = index < 3 ? 150f : 436f;
            return (x, y);
        }

        /// <summary>
        /// 每格画同一份内容：亮底（深浅两块）+ 三个 alpha 阶梯的叠压精灵 + 白 / 彩 tint 各一块 + 一个 alpha 脉动块。
        /// 同一份内容换不同混合状态，差异就只可能来自混合本身。
        /// </summary>
        private void DrawCellContent(SpriteBatch batch, Texture2D tex, float x, float y, float w, float h)
        {
            // 亮底分左右两块（浅 / 深）：加色与正片叠底只有在"底"上才看得出来
            float half = w * 0.5f;
            batch.Draw(KDefaultRes.DefaultTexture2D,
                new Rectangle((int)x, (int)y, (int)half, (int)h), null, new Color(96, 140, 220));
            batch.Draw(KDefaultRes.DefaultTexture2D,
                new Rectangle((int)(x + half), (int)y, (int)(w - half), (int)h), null, new Color(34, 48, 78));

            // 三个 alpha 阶梯（64 / 144 / 224）的棋盘精灵错位叠压：叠压关系与 alpha 权重一眼可见
            // 注：Color 的 4 参构造有 (int,int,int,int) 与 (byte,byte,byte,byte) 两个重载，参数混用会二义，
            // 故这里统一给 int（byte 会被隐式拓宽）。
            for (int i = 0; i < 3; i++)
            {
                batch.Draw(tex,
                    new Vector2(x + 16f + i * 36f, y + 24f + i * 24f),
                    color: new Color(255, 190, 90, 64 + i * 80));
            }

            // 白色 tint（直通原纹理）与彩色 tint（走按颜色缓存的着色副本）：顺手对照顶点色两路
            batch.Draw(tex, new Vector2(x + 16f, y + h - 74f), scale: new Vector2(0.7f, 0.7f));
            batch.Draw(tex, new Vector2(x + 70f, y + h - 74f), scale: new Vector2(0.7f, 0.7f), color: new Color(140, 255, 170));

            // alpha 随帧脉动的小块：证明顶点 alpha 是逐帧生效的（不是一次性烘焙）
            int pulse = (int)(40f + 215f * (0.5f + 0.5f * Math.Sin(_frame * 0.05f)));
            batch.Draw(tex, new Vector2(x + w - 60f, y + 24f), scale: new Vector2(0.6f, 0.6f),
                color: new Color(255, 255, 255, pulse));
        }

        private void DrawNotes(SpriteBatch batch, float y)
        {
            y = DrawLine(batch, "Canvas2D 只有一个 globalCompositeOperation，后端按源/目标因子归类：",
                28f, y, new Color(150, 165, 195));
            y = DrawLine(batch, "禁用混合与其余 → source-over；SrcAlpha+One、One+One → lighter；DstColor+Zero、Zero+SrcColor → multiply",
                28f, y, new Color(150, 165, 195));
            y = DrawLine(batch, "已知差异：没有「忽略源 alpha」的算子，⑥ Opaque 下顶点 alpha 仍生效（半透明块照旧半透明）",
                28f, y, new Color(255, 206, 110));
            DrawLine(batch, "传奇客户端只用 NonPremultiplied / Additive / Multiply（采样恒 PointClamp），这三路都在上面；它真正的瓶颈是离屏渲染目标",
                28f, y, new Color(150, 165, 195));
        }
    }

}
