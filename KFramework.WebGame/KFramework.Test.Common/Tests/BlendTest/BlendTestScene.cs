using System.Collections.Generic;

using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.BlendTest
{

    /// <summary>
    /// 混合模式（BlendState）可视化测试。
    /// <para>模式①（默认）：每一格先把同一张彩色底图用 Opaque 画出真实图像作参照，再用指定的混合模式叠一层叠加图，
    /// 直观对比各混合的效果；重点演示传奇 / 传奇昼夜系统用的 <see cref="BlendState.Multiply"/>（Zero, SourceColor）。</para>
    /// <para>模式②：用 3 个 Draw 直接画 3 层 / 3 种颜色演示「半透明层漏到下层」。底层红(最大·不透明) / 中层绿(中等·不透明) / 顶层蓝(最小·半透明)。
    /// 下面两层不透明，只有最顶层是半透明；半透明蓝和正下方的绿混合，若蓝底下没有不透明绿底垫就会漏到更下面的红。</para>
    /// </summary>
    public sealed class BlendTestScene : TestSceneBase
    {
        private Texture2D _baseTex = null!;     // 彩色底图（模拟游戏场景）
        private Texture2D _whiteTex = null!;    // 纯白（用于着色成半透明/加法/覆盖叠加）
        private Texture2D _mirLightTex = null!; // 传奇光照图：暗度灰铺底 + 中心火把白光池（RGB 烘焙）

        // —— 模式②：3 层 / 3 色 复现「半透明层压在透明层上漏到最底层」——
        private static readonly byte[] AlphaLevels = [100, 150, 215]; // 顶层半透明蓝的浓度（≈0.4 / 0.6 / 0.84）

        private int _mode;                      // 0 = 通用混合网格；1 = 半透明漏下层（3 层演示）
        private int _alphaIndex = 1;

        private readonly List<IDisposable> _owned = [];

        private sealed record BlendDemo(string Name, BlendState Blend, Texture2D? Overlay, Color Tint);

        public override string Title => "混合模式 BlendState 测试（含传奇昼夜乘法压暗 + 3 层半透明漏下层演示）";

        private T Own<T>(T d) where T : IDisposable { _owned.Add(d); return d; }

        public override void LoadContent()
        {
            const int w = 256, h = 256;
            _baseTex = Own(MakeBaseTexture(Device, w, h));
            _whiteTex = Own(MakeSolidTexture(Device, 4, 4, 255, 255, 255, 255));
            _mirLightTex = Own(MakeMirLightTexture(Device, w, h));
        }

        public override void Update()
        {
            // 基类处理 Esc / 左上角返回；一旦切走就不要再碰本场景的状态。
            base.Update();
            if (!ReferenceEquals(KSceneMgr.Main, this)) return;

            // 按钮命中（_buttons 由上一帧 DrawBody 里的 BeginButtons/AddUiButton 建好，布局稳定，可直接用）。
            ClickButtons();
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            // 基类 Draw() 已用默认混合 Begin 并画了返回/标题，这里先收掉，再按各模式开关批次。
            batch.End();

            // —— 顶部模式切换按钮（两种模式都显示）——
            BeginButtons((int)origin.Y);
            AddUiButton("① 通用混合模式", () => _mode = 0, _mode == 0);
            AddUiButton("② 半透明漏下层(3层)", () => _mode = 1, _mode == 1);
            if (_mode == 1)
                AddUiButton($"半透明浓度 {AlphaLevels[_alphaIndex] * 100 / 255}% [点击]", CycleAlpha);

            if (_mode == 1)
            {
                DrawLeakDemo(batch, origin);
                batch.Begin();                  // 重新开批次给 DrawButtons / 基类 End 收尾
            }
            else
            {
                DrawGrid(batch, origin);        // DrawGrid 末尾已自行 Begin
            }

            DrawButtons(batch);
        }

        // ===== 模式①：通用混合模式网格（原逻辑，仅起始 y 让位给按钮） =====

        private void DrawGrid(SpriteBatch batch, Vector2 origin)
        {
            List<BlendDemo> demos = BuildDemos();

            const int cell = 200, hGap = 28, vGap = 38, labelH = 56;
            int cols = Math.Max(1, (int)((Device.Viewport.Width - origin.X) / (cell + hGap)));
            int i = 0;
            float x = origin.X, y = ContentTop + 6; // 顶部留点空隙，且避开上方按钮
            float contentBottom = y;

            foreach (BlendDemo d in demos)
            {
                // 名称（默认混合绘制，保证始终可读）
                batch.Begin();
                DrawName(batch, d.Name, x, y);
                batch.End();

                Rectangle img = new((int)x, (int)(y + labelH), cell, cell);
                contentBottom = Math.Max(contentBottom, img.Bottom);

                // 底图：始终用 Opaque 画出真实图像作为参照
                batch.Begin();
                batch.Draw(_baseTex, img, null, Color.White);
                batch.End();

                // 叠加层：用该 demo 指定的混合模式
                if (d.Overlay != null)
                {
                    batch.Begin(SpriteSortMode.Deferred, d.Blend);
                    batch.Draw(d.Overlay, img, null, d.Tint);
                    batch.End();
                }

                if (++i % cols == 0) { x = origin.X; y += cell + labelH + vGap; }
                else x += cell + hGap;
            }

            // 说明行（放在已绘制内容下方，避免压到最后一行）
            batch.Begin();
            batch.DrawString(Font,
                "传奇昼夜 = Multiply(ZERO, SRC_COLOR) = 主画面 × 光照图；光照图 = 暗度灰 + 火把白光池。",
                new Vector2(origin.X, contentBottom + vGap + 4), new Color(150, 200, 255));
            batch.End();

            // 留一个开启的批次给基类 Draw() 的 batch.End() 收尾（保持 Begin/End 配对）
            batch.Begin();
        }

        private List<BlendDemo> BuildDemos()
        {
            var list = new List<BlendDemo>
            {
                new("原图(参考)", BlendState.Opaque, null, Color.White),
                new("NonPremultiplied\n半透明叠加", BlendState.NonPremultiplied, _whiteTex, new Color(255, 140, 0, 128)),
                new("AlphaBlend\n预乘透明", BlendState.AlphaBlend, _whiteTex, new Color(255, 140, 0, 128)),
                new("Additive\n加法发光", BlendState.Additive, _whiteTex, new Color(255, 160, 40, 255)),
                new("Opaque\n覆盖替换", BlendState.Opaque, _whiteTex, new Color(255, 140, 0, 255)),
                new("Multiply\n乘法压暗(灰)", BlendState.Multiply, _whiteTex, new Color(70, 80, 100, 255)),
                new("传奇Multiply\n暗度+火把光池", BlendState.Multiply, _mirLightTex, Color.White),
                // 用公开构造把混合参数直接传进来：Final = Dest*SrcColor + Src*(1-SrcColor)
                new("自定义混合\nSrcColor/1-SrcColor",
                    new BlendState(BlendMode.SrcColor, BlendMode.OneMinusSrcColor, BlendMode.One, BlendMode.OneMinusSrcAlpha),
                    _whiteTex, new Color(255, 140, 0, 255)),
            };
            return list;
        }

        private void DrawName(SpriteBatch batch, string name, float x, float y)
        {
            string[] lines = name.Split('\n');
            float yy = y;
            foreach (string line in lines)
            {
                batch.DrawString(Font, line, new Vector2(x, yy), new Color(220, 230, 245));
                yy += Font.LineSpacing + 2f;
            }
        }

        // ===== 模式②：3 层 / 3 色 半透明漏下层演示（不用 RT，就 3 个 Draw） =====
        // 底红(最大·不透明) / 中绿(中等·不透明) / 顶蓝(最小·半透明)。

        private void CycleAlpha()
        {
            _alphaIndex = (_alphaIndex + 1) % AlphaLevels.Length;
        }

        private void DrawLeakDemo(SpriteBatch batch, Vector2 origin)
        {
            int a = AlphaLevels[_alphaIndex];
            int pw = 360, ph = 240;
            int x = (int)origin.X, y = (int)(ContentTop + 40);

            // 就这 3 个 Draw：红(满铺·不透明) → 绿(居中缩小·不透明) → 蓝(再缩小·半透明)
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(x, y, pw, ph), null, new Color(220, 70, 70));                            // ① 底：红 · 不透明 · 最大
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(x + 50, y + 50, pw - 100, ph - 100), null, new Color(70, 200, 110));      // ② 中：绿 · 不透明
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(x + 100, y + 100, pw - 200, ph - 200), null, new Color(90, 150, 240, a));  // ③ 顶：蓝 · 半透明
            batch.End();

            batch.Begin();
            batch.DrawString(Font, "3 层：底红(最大·不透明) / 中绿(中等·不透明) / 顶蓝(最小·半透明) —— 下面两层不透明，只有最顶层是半透明。",
                new Vector2(x, y - 30), new Color(255, 206, 110));
            batch.DrawString(Font, "顶层蓝是半透明的，它和正下方的绿混合成蓝绿色；如果蓝底下没有不透明绿底垫，就会漏到更下面的红。",
                new Vector2(x, y + ph + 12), new Color(150, 200, 255));
            batch.DrawString(Font, "点「半透明浓度」可看不同 alpha 下蓝的透出程度；按 Esc 返回总屏。",
                new Vector2(x, y + ph + 40), Color.LightGray);
            batch.End();
        }

        // —— 程序化生成纹理（不依赖任何图片资源） ——

        private static Texture2D MakeBaseTexture(GraphicsDevice device, int w, int h)
        {
            byte[] data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    bool left = x < w / 2, top = y < h / 2;
                    byte r, g, b;
                    if (left && top) { r = 235; g = 60; b = 60; }        // 红
                    else if (!left && top) { r = 60; g = 200; b = 80; }   // 绿
                    else if (left && !top) { r = 60; g = 120; b = 235; }  // 蓝
                    else { r = 235; g = 210; b = 70; }                    // 黄
                    // 中心白块（光源参考）
                    int cx = x - w / 2, cy = y - h / 2;
                    if (cx * cx + cy * cy < 28 * 28) { r = 255; g = 255; b = 255; }
                    data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = 255;
                }
            }
            return device.CreateTexture(w, h, data);
        }

        private static Texture2D MakeSolidTexture(GraphicsDevice device, int w, int h, byte r, byte g, byte b, byte a)
        {
            byte[] data = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                data[i * 4] = r; data[i * 4 + 1] = g; data[i * 4 + 2] = b; data[i * 4 + 3] = a;
            }
            return device.CreateTexture(w, h, data);
        }

        private static Texture2D MakeMirLightTexture(GraphicsDevice device, int w, int h)
        {
            // 传奇光照图：暗度灰铺底（夜晚压暗），中心一个白色火把光池（局部提亮）。
            // 用 Multiply 合成回主画面时：Final = 主画面 × 本图 → 整体压暗，中心火把处保持明亮。
            byte[] data = new byte[w * h * 4];
            const byte dr = 55, dg = 60, db = 75; // 夜晚暗度（接近纯暗）
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    int cx = x - w / 2, cy = y - h / 2;
                    double d = System.Math.Sqrt(cx * cx + cy * cy);
                    double t = System.Math.Max(0, 1 - d / 80); // 0..1 光池强度
                    data[i] = (byte)(dr + (255 - dr) * t);
                    data[i + 1] = (byte)(dg + (255 - dg) * t);
                    data[i + 2] = (byte)(db + (255 - db) * t);
                    data[i + 3] = 255;
                }
            }
            return device.CreateTexture(w, h, data);
        }

        public override void Dispose()
        {
            foreach (IDisposable d in _owned) d.Dispose();
            _owned.Clear();
            base.Dispose();
        }
    }

}
