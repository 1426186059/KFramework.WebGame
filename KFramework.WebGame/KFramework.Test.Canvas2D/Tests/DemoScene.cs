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

        // ================================================================
        // 参考图案生成（不依赖任何资源文件，纯程序化）
        // 照 KFramework.Test.WebGPU/Tests/DemoScene.cs 移植一份，保持跨后端同一套参考图。
        // ================================================================

        /// <summary>
        /// 程序化生成一张带光照 + 高光的球（白色，绘制时用 Color 着色），RGBA8。
        /// <para>
        /// 曲线边缘是检验抗锯齿最理想的形状；高光固定在<b>左上</b>，所以离屏画面若上下颠倒，
        /// 高光会跑到左下 —— 不用额外标记也能看出来。
        /// </para>
        /// </summary>
        protected Texture2D MakeBall(int size)
        {
            var pixels = new byte[size * size * 4];

            // 光方向（左上前）与 Blinn 半程向量
            var light = Vector3.Normalize(new Vector3(-0.45f, -0.55f, 0.70f));
            var half = Vector3.Normalize(light + new Vector3(0f, 0f, 1f));

            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - radius) / radius;
                    float ny = (y + 0.5f - radius) / radius;
                    float dist = MathF.Sqrt(nx * nx + ny * ny);

                    // 边缘一个像素的抗锯齿
                    float alpha = Math.Clamp((1f - dist) * size * 0.5f, 0f, 1f);

                    float nz = MathF.Sqrt(MathF.Max(0f, 1f - MathF.Min(dist, 1f) * MathF.Min(dist, 1f)));
                    var normal = Vector3.Normalize(new Vector3(nx, ny, nz));

                    float diffuse = MathF.Max(0f, Vector3.Dot(normal, light));
                    float specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, half)), 28f);
                    float v = 0.28f + 0.68f * diffuse + 0.85f * specular;

                    int offset = (y * size + x) * 4;
                    byte c = (byte)Math.Clamp(v * 255f, 0f, 255f);
                    pixels[offset + 0] = c;
                    pixels[offset + 1] = c;
                    pixels[offset + 2] = c;
                    pixels[offset + 3] = (byte)(alpha * 255f);
                }
            }

            return Device.CreateTexture(size, size, pixels);
        }

        /// <summary>HSV → Color（h: 0..360，s / v: 0..1），用于给球批量着色。</summary>
        protected static Color Hsv(float h, float s, float v)
        {
            float c = v * s;
            float x2 = c * (1f - MathF.Abs(h / 60f % 2f - 1f));
            float m = v - c;
            float r = 0f, g = 0f, b = 0f;

            if (h < 60f) { r = c; g = x2; }
            else if (h < 120f) { r = x2; g = c; }
            else if (h < 180f) { g = c; b = x2; }
            else if (h < 240f) { g = x2; b = c; }
            else if (h < 300f) { r = x2; b = c; }
            else { r = c; b = x2; }

            return new Color((int)((r + m) * 255f), (int)((g + m) * 255f), (int)((b + m) * 255f));
        }

        /// <summary>
        /// 纵向明暗渐变（宽只需几像素，横向拉伸时按列复制即可）。
        /// 比棋盘格柔和，作为面板底色不会干扰对球体边缘的观察。
        /// </summary>
        protected Texture2D MakeVGradient(int width, int height, Color top, Color bottom)
        {
            var pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                float t = height <= 1 ? 0f : y / (float)(height - 1);
                byte r = (byte)(top.R + (bottom.R - top.R) * t);
                byte g = (byte)(top.G + (bottom.G - top.G) * t);
                byte b = (byte)(top.B + (bottom.B - top.B) * t);

                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    pixels[i] = r; pixels[i + 1] = g; pixels[i + 2] = b; pixels[i + 3] = 255;
                }
            }

            return Device.CreateTexture(width, height, pixels);
        }
    }

}
