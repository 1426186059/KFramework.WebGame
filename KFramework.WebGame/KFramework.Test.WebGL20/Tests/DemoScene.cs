using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{

    /// <summary>
    /// 测试页基类：统一画标题 / 底部切换提示，子类按需重写
    /// <see cref="RenderOffscreen"/>（离屏渲染，自行 Begin / End）与 <see cref="DrawBody"/>（屏幕正文）。
    /// <para>
    /// 注意：切换渲染目标（<c>SetRenderTarget</c>）必须在 SpriteBatch 的 Begin / End <b>之外</b>进行，
    /// 故需要离屏渲染的页面统一在 <see cref="RenderOffscreen"/> 里做。
    /// 需要多批不同状态（例如逐个混合模式）的页面可直接重写 <see cref="Draw"/>，
    /// 复用 <see cref="DrawHeader"/> / <see cref="DrawFooter"/>。
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

        /// <summary>绕 UV 中心 (0.5, 0.5) 旋转的矩阵（行向量约定：p' = p × M，平移在 M41 / M42）。</summary>
        protected static Matrix4x4 RotationAboutCenter(float angle)
        {
            float cs = MathF.Cos(angle);
            float sn = MathF.Sin(angle);

            Matrix4x4 m = Matrix4x4.Identity;
            m.M11 = cs; m.M12 = sn;
            m.M21 = -sn; m.M22 = cs;
            m.M41 = 0.5f - (0.5f * cs - 0.5f * sn);
            m.M42 = 0.5f - (0.5f * sn + 0.5f * cs);
            return m;
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
