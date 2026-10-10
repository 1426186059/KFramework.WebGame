using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>
    /// 测试页基类（与 WebGL 测试工程同构）：统一画标题 / 底部切换提示，子类按需重写
    /// <see cref="RenderOffscreen"/>（Canvas2D 的离屏目标是另一块 canvas，与画布同构，钩子仍为排版一致而保留）
    /// 与 <see cref="DrawBody"/>（屏幕正文）。
    /// </summary>
    public abstract class DemoScene : KSceneBase
    {
        public abstract string Title { get; }

        protected GraphicsDevice Device => KSceneMgr.Game.GraphicsDevice;

        protected SpriteBatch Batch => KSceneMgr.SpriteBatch;

        protected IFont Font => KDefaultRes.DefaultSpriteFont;

        /// <summary>页内自述文字（显示在标题下方）。</summary>
        protected virtual string Description => string.Empty;

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            RenderOffscreen(batch);

            batch.Begin();
            DrawHeader(batch);
            DrawBody(batch, 84f);
            DrawFooter(batch);
            batch.End();
        }

        protected void DrawHeader(SpriteBatch batch)
        {
            batch.DrawString(Font, Title, new Vector2(28f, 20f), new Color(126, 200, 255));
            if (Description.Length > 0)
                batch.DrawString(Font, Description, new Vector2(28f, 48f), new Color(150, 165, 195));
        }

        protected void DrawFooter(SpriteBatch batch)
        {
            batch.DrawString(Font, "按数字键切换测试页，Esc 返回总纲",
                new Vector2(28f, Device.Viewport.Height - 38f), new Color(110, 130, 170));
        }

        /// <summary>离屏渲染阶段（在 Begin / End 之外；Canvas2D 的离屏同样是画进另一块 canvas，保留钩子与 WebGL 工程同构）。</summary>
        protected virtual void RenderOffscreen(SpriteBatch batch) { }

        /// <summary>屏幕正文（已在 Begin / End 之间）。</summary>
        protected virtual void DrawBody(SpriteBatch batch, float top) { }

        /// <summary>画一行文本，返回下一行的 y。</summary>
        protected float DrawLine(SpriteBatch batch, string text, float x, float y, Color color)
        {
            batch.DrawString(Font, text, new Vector2(x, y), color);
            return y + Font.LineSpacing + 6f;
        }

        /// <summary>在同一行里接着画一段文本（可指定字体），并把光标 x 往后推；返回新的 x。</summary>
        protected float DrawInline(SpriteBatch batch, IFont font, string text, float x, float y, Color color)
        {
            batch.DrawString(font, text, new Vector2(x, y), color);
            return x + font.Measure(text).X + 16f;
        }

        /// <summary>程序化生成一张棋盘格纹理（不依赖任何资源文件）。</summary>
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
                    data[i] = c.R;
                    data[i + 1] = c.G;
                    data[i + 2] = c.B;
                    data[i + 3] = c.A;
                }
            }

            texture.SetData(data);
            return texture;
        }
    }

}
