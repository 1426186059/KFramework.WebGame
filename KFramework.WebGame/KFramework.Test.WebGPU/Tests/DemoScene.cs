using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{

    /// <summary>
    /// 测试页基类：统一画标题 / 页内自述 / 底部切换提示，并提供「返回总纲」（Esc 或点左上按钮）。
    /// <para>
    /// 子类重写 <see cref="DrawBody"/> 画正文；需要多批不同状态（例如逐个混合模式）的页面
    /// 可直接重写 <see cref="Draw"/>，复用 <see cref="DrawHeader"/> / <see cref="DrawFooter"/>。
    /// </para>
    /// <para>
    /// 图案生成放在基类（<see cref="MakeTestChart"/> / <see cref="MakeAlphaChart"/> / <see cref="MakeChecker"/>），
    /// 各页共用同一张参考图，便于跨后端、跨页面直接比对。
    /// </para>
    /// </summary>
    public abstract class DemoScene : KSceneBase
    {
        public abstract string Title { get; }

        protected GraphicsDevice Device => KSceneMgr.Game.GraphicsDevice;

        protected SpriteBatch Batch => KSceneMgr.SpriteBatch;

        protected IFont Font => KDefaultRes.DefaultSpriteFont;

        /// <summary>页内自述文字（显示在标题下方）。</summary>
        protected virtual string Description => string.Empty;

        /// <summary>底部提示文字。</summary>
        protected virtual string Hint => "按 1 / 2 / 3 直接切换，Esc 或点左上「← 总纲」返回";

        private readonly Rectangle _backRect = new(20, 16, 104, 34);

        /// <summary>
        /// 页内更新。<b>密封</b>：返回按钮的命中处理必须无条件执行。
        /// <para>
        /// 踩过的坑：子类直接重写 <c>Update()</c> 且不调 <c>base.Update()</c>，
        /// 于是该页的「← 总纲」按钮（以及原先写在基类里的 Esc）全部失灵 ——
        /// 症状只在某一页出现，很难联想到是重写没调基类。改为密封后子类只能重写
        /// <see cref="UpdateBody"/>，基类行为再也绕不过去。
        /// </para>
        /// </summary>
        public sealed override void Update()
        {
            // Esc 的全局切换由宿主 WebGpuTestGame 统一处理（在任何测试页都能回总纲），这里只管按钮点击。
            if (Input_Mouse.GetButtonDown(MouseButton.Left) && _backRect.Contains(Input_Mouse.Position))
                MainScene.Open();

            UpdateBody();
        }

        /// <summary>页内自定义逻辑的扩展点（计帧、切模式等）。基类已在 Update 里先处理完返回按钮。</summary>
        protected virtual void UpdateBody() { }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            // 离屏渲染阶段（自行 Begin / End）：切换渲染目标必须在批次的 Begin / End 之外。
            RenderOffscreen(batch);

            batch.Begin();
            DrawBack(batch);
            DrawHeader(batch);
            DrawBody(batch, 84f);
            DrawFooter(batch);
            batch.End();
        }

        private void DrawBack(SpriteBatch batch)
        {
            batch.Draw(KDefaultRes.DefaultTexture2D, _backRect, new Color(38, 52, 92));
            batch.DrawString(Font, "← 总纲", new Vector2(_backRect.X + 12, _backRect.Y + 6), new Color(170, 210, 255));
        }

        protected void DrawHeader(SpriteBatch batch)
        {
            batch.DrawString(Font, Title, new Vector2(_backRect.Right + 18, 20f), new Color(126, 200, 255));
            if (Description.Length > 0)
                batch.DrawString(Font, Description, new Vector2(28f, 48f), new Color(150, 165, 195));
        }

        protected void DrawFooter(SpriteBatch batch)
        {
            batch.DrawString(Font, Hint, new Vector2(28f, Device.Viewport.Height - 38f), new Color(110, 130, 170));
        }

        /// <summary>离屏渲染阶段（默认不做）。</summary>
        protected virtual void RenderOffscreen(SpriteBatch batch) { }

        /// <summary>屏幕正文（已在 Begin / End 之间）。</summary>
        protected virtual void DrawBody(SpriteBatch batch, float top) { }

        /// <summary>画一行文本，返回下一行的 y。</summary>
        protected float DrawLine(SpriteBatch batch, string text, float x, float y, Color color)
        {
            batch.DrawString(Font, text, new Vector2(x, y), color);
            return y + Font.LineSpacing + 6f;
        }

        // ================================================================
        // 参考图案生成（不依赖任何资源文件，纯程序化）
        // ================================================================

        /// <summary>
        /// 综合参考图：一张图同时检验颜色、灰阶、过滤/走样、曲线边缘、方向与混合。
        /// <list type="bullet">
        ///   <item><description>顶部彩条：白/黄/青/绿/品红/红/蓝/黑（标准色带）</description></item>
        ///   <item><description>灰阶条：检验亮度映射</description></item>
        ///   <item><description>1px 网格：检验放大/缩小时的过滤与走样</description></item>
        ///   <item><description>同心圆环：曲线硬边缘，MSAA 是否生效一眼可辨</description></item>
        ///   <item><description>四角方位标记（红/绿/蓝/黄）：上下翻转或左右镜像立刻暴露</description></item>
        ///   <item><description>底部半透明横条：检验 alpha 混合</description></item>
        /// </list>
        /// </summary>
        protected Texture2D MakeTestChart(int size)
        {
            var texture = Device.CreateTexture(size, size);
            var data = new byte[size * size * 4];

            void Put(int x, int y, byte r, byte g, byte b, byte a)
            {
                int i = (y * size + x) * 4;
                data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = a;
            }

            int barH = Math.Max(1, size / 8);
            int bodyTop = barH * 2;

            // ① 彩条 + ② 灰阶
            for (int y = 0; y < bodyTop; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (y < barH)
                    {
                        int idx = Math.Min(7, x * 8 / size);
                        switch (idx)
                        {
                            case 0: Put(x, y, 255, 255, 255, 255); break;   // 白
                            case 1: Put(x, y, 255, 255, 0, 255); break;     // 黄
                            case 2: Put(x, y, 0, 255, 255, 255); break;     // 青
                            case 3: Put(x, y, 0, 255, 0, 255); break;       // 绿
                            case 4: Put(x, y, 255, 0, 255, 255); break;     // 品红
                            case 5: Put(x, y, 255, 0, 0, 255); break;       // 红
                            case 6: Put(x, y, 0, 0, 255, 255); break;       // 蓝
                            default: Put(x, y, 0, 0, 0, 255); break;        // 黑
                        }
                    }
                    else
                    {
                        byte v = (byte)(255 - (x * 255 / Math.Max(1, size - 1)));
                        Put(x, y, v, v, v, 255);
                    }
                }
            }

            // ③ 主体背景
            for (int y = bodyTop; y < size; y++)
                for (int x = 0; x < size; x++)
                    Put(x, y, 44, 52, 74, 255);

            // ④ 1px 网格
            int step = Math.Max(2, size / 16);
            for (int p = 0; p < size; p += step)
            {
                if (p >= bodyTop)
                    for (int x = 0; x < size; x++) Put(x, p, 92, 104, 130, 255);
                for (int y = bodyTop; y < size; y++) Put(p, y, 92, 104, 130, 255);
            }

            // ⑤ 同心圆环（硬边缘 → 走样可见；开 MSAA 后应明显平滑）
            float cx = size * 0.5f, cy = size * 0.62f;
            float outer = size * 0.24f, inner = size * 0.16f, core = size * 0.08f;
            for (int y = bodyTop; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d <= outer && d > inner) Put(x, y, 232, 240, 255, 255);   // 环
                    if (d <= core) Put(x, y, 240, 170, 90, 255);                  // 圆心
                }
            }

            // ⑥ 四角方位标记
            int mark = Math.Max(4, size / 10);
            Fill(data, size, 0, 0, mark, mark, 255, 60, 60, 255);                          // 左上 红
            Fill(data, size, size - mark, 0, mark, mark, 90, 220, 120, 255);                // 右上 绿
            Fill(data, size, 0, size - mark, mark, mark, 90, 150, 255, 255);                // 左下 蓝
            Fill(data, size, size - mark, size - mark, mark, mark, 250, 220, 90, 255);      // 右下 黄

            // ⑦ 底部半透明横条
            int stripH = Math.Max(2, size / 12);
            for (int y = size - stripH; y < size; y++)
                for (int x = 0; x < size; x++)
                    Put(x, y, 255, 255, 255, 128);

            texture.SetData(data);
            return texture;
        }

        /// <summary>横向 alpha 渐变（左不透明 → 右全透明），用于对比各混合模式。</summary>
        protected Texture2D MakeAlphaChart(int size)
        {
            var texture = Device.CreateTexture(size, size);
            var data = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = (y * size + x) * 4;
                    byte a = (byte)(255 - (x * 255 / Math.Max(1, size - 1)));
                    data[i] = 255; data[i + 1] = 255; data[i + 2] = 255; data[i + 3] = a;
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>程序化生成一张棋盘格纹理。</summary>
        protected Texture2D MakeChecker(int size, Color a, Color b)
        {
            var texture = Device.CreateTexture(size, size);
            var data = new byte[size * size * 4];
            int cell = Math.Max(1, size / 8);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = ((x / cell) + (y / cell)) % 2 == 0 ? a : b;
                    int i = (y * size + x) * 4;
                    data[i] = c.R; data[i + 1] = c.G; data[i + 2] = c.B; data[i + 3] = c.A;
                }
            }

            texture.SetData(data);
            return texture;
        }

        private static void Fill(byte[] data, int size, int x0, int y0, int w, int h,
                                 byte r, byte g, byte b, byte a)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(size, y0 + h); y++)
            {
                for (int x = Math.Max(0, x0); x < Math.Min(size, x0 + w); x++)
                {
                    int i = (y * size + x) * 4;
                    data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = a;
                }
            }
        }
    }

}
