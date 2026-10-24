using System.Collections.Generic;

using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.BlendTest
{

    /// <summary>
    /// 混合模式（BlendState）可视化测试。
    /// <para>模式①（默认）：每一格先把同一张彩色底图用 Opaque 画出真实图像作参照，再用指定的混合模式叠一层叠加图，
    /// 直观对比各混合的效果；重点演示传奇 / 传奇昼夜系统用的 <see cref="BlendState.Multiply"/>（Zero, SourceColor）。</para>
    /// <para>模式②：3 层 / 3 色 演示。底层红(最大·不透明) / 中层绿(中等·不透明) / 顶层蓝(最小·半透明)，下面两层不透明、只有最顶层半透明。
    /// 把同样的 3 层用不同数量的 RT 渲染并排对比（每行一组，右侧最终图对齐）：A 3 个 Draw 直接画（无 RT）；B 1 张 RT（三层合一）；C 2 张 RT（底 / 中上）；D 3 张 RT（每层各自）。</para>
    /// </summary>
    public sealed class BlendTestScene : TestSceneBase
    {
        private Texture2D _baseTex = null!;     // 彩色底图（模拟游戏场景）
        private Texture2D _whiteTex = null!;    // 纯白（用于着色成半透明/加法/覆盖叠加）
        private Texture2D _mirLightTex = null!; // 传奇光照图：暗度灰铺底 + 中心火把白光池（RGB 烘焙）

        // —— 模式②：3 层 / 3 色 演示（4 组对照）——
        // 同一组「底红(最大·不透明)/中绿(中等·不透明)/顶蓝(最小·半透明)」，用不同数量的 RT 渲染，横向对比结果：
        // ① 3 个 Draw 直接画（无 RT）；② 1 张 RT（三层合一）；③ 2 张 RT（底 / 中上）；④ 3 张 RT（每层各自）。
        private static readonly byte[] AlphaLevels = [40, 80, 120, 160, 200, 240]; // 顶层半透明蓝的浓度（≈0.16 / 0.31 / 0.47 / 0.63 / 0.78 / 0.94）

        // 三层的配色方案（底·最大不透明 / 中·中等不透明 / 顶·最小半透明）。可点击切换，方便看清层间漏下效果。
        private sealed record ColorScheme(string Name, Color Bottom, Color Mid, Color Top);
        private static readonly ColorScheme[] ColorSchemes = [
            new("红/绿/蓝",   new Color(220, 70, 70),  new Color(70, 200, 110), new Color(90, 150, 240)),
            new("黄/品红/青", new Color(235, 210, 70), new Color(220, 70, 200), new Color(70, 200, 235)),
            new("橙/紫/青绿", new Color(255, 120, 40), new Color(140, 80, 220), new Color(40, 200, 160)),
            new("红/黄/蓝",   new Color(240, 60, 60),  new Color(240, 200, 40), new Color(60, 140, 240)),
            new("绿/红/粉",   new Color(60, 200, 120), new Color(230, 90, 90),  new Color(230, 140, 230)),
        ];

        private const int RT_W = 360, RT_H = 240; // 烘焙 RT 的设计分辨率（3:2）
        private RenderTarget2D _rtAll = null!;     // ② 三层合一 RT
        private RenderTarget2D _rtBottom = null!;  // ③/④ 底层红 RT
        private RenderTarget2D _rtMidTop = null!;  // ③ 中+顶（绿+蓝）RT
        private RenderTarget2D _rtGreen = null!;   // ④ 中层绿 RT
        private RenderTarget2D _rtBlue = null!;    // ④ 顶层蓝（半透明）RT
        private bool _rtDirty = true;

        private int _mode;                      // 0 = 通用混合网格；1 = 半透明漏下层（3 层演示）
        private int _alphaIndex = 1;
        private int _colorSchemeIndex;          // 当前配色方案

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

            // 模式② 的 5 张 RT 固定设计分辨率，内容按需烘焙。
            _rtAll = Own(new RenderTarget2D(Device, RT_W, RT_H));
            _rtBottom = Own(new RenderTarget2D(Device, RT_W, RT_H));
            _rtMidTop = Own(new RenderTarget2D(Device, RT_W, RT_H));
            _rtGreen = Own(new RenderTarget2D(Device, RT_W, RT_H));
            _rtBlue = Own(new RenderTarget2D(Device, RT_W, RT_H));
            _rtDirty = true;
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
            {
                AddUiButton($"半透明浓度 {AlphaLevels[_alphaIndex] * 100 / 255}% [点击]", CycleAlpha);
                AddUiButton($"配色：{ColorSchemes[_colorSchemeIndex].Name} [点击切换]", CycleColorScheme);
            }

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
            _rtDirty = true;
        }

        private void CycleColorScheme()
        {
            _colorSchemeIndex = (_colorSchemeIndex + 1) % ColorSchemes.Length;
            _rtDirty = true;
        }

        // 当前配色下、给定顶层 alpha 的三层颜色（顶层带 alpha，底/中不透明）。
        private (Color bottom, Color mid, Color top) LayerColors(int a)
        {
            ColorScheme s = ColorSchemes[_colorSchemeIndex];
            return (s.Bottom, s.Mid, s.Top.WithAlpha((byte)a));
        }

        // 画 3 层：底(最大·不透明) / 中(中等·不透明) / 顶(最小·半透明)。颜色由配色方案决定；inset 按比例。
        private static void DrawThreeLayers(SpriteBatch b, int x, int y, int w, int h, Color cBottom, Color cMid, Color cTop)
        {
            int gi = (int)(w * 0.14f + 0.5f); // 中层内缩
            int bi = (int)(w * 0.28f + 0.5f); // 顶层内缩
            int half = w / 2;                  // 底图左半亮、右半暗，便于辨认"底是否透出"
            b.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(x, y, half, h), null, cBottom);
            b.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(x + half, y, w - half, h), null, cBottom.Multiply(0.45f));
            b.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(x + gi, y + gi, w - 2 * gi, h - 2 * gi), null, cMid);
            b.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(x + bi, y + bi, w - 2 * bi, h - 2 * bi), null, cTop);
        }

        // 烘焙 5 张 RT（组②③④ 用）。统一用 BlendState.NonPremultiplied_Fix（颜色预乘、alpha 直线）+ 透明清屏，
        // 显示端用 AlphaBlend 读取；这样 RT 内是「预乘 rgb + 直线 alpha」，与不拆 RT 直接画逐像素一致。
        private void BakeAllRTs()
        {
            int a = AlphaLevels[_alphaIndex];
            int gi = (int)(RT_W * 0.14f + 0.5f);
            int bi = (int)(RT_W * 0.28f + 0.5f);
            var green = new Rectangle(gi, gi, RT_W - 2 * gi, RT_H - 2 * gi);
            var blue = new Rectangle(bi, bi, RT_W - 2 * bi, RT_H - 2 * bi);
            (Color cRed, Color cGreen, Color cBlue) = LayerColors(a);
            var cRedDark = cRed.Multiply(0.45f); // 底图右半刷暗色，便于辨认"底是否透出"

            // ② 三层合一 RT
            Device.SetRenderTarget(_rtAll); Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied_Fix);
            Batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(0, 0, RT_W / 2, RT_H), null, cRed);
            Batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(RT_W / 2, 0, RT_W - RT_W / 2, RT_H), null, cRedDark);
            Batch.Draw(KDefaultRes.DefaultTexture2D, green, null, cGreen);
            Batch.Draw(KDefaultRes.DefaultTexture2D, blue, null, cBlue);
            Batch.End(); Device.SetRenderTarget(null);

            // ③ 底层红 RT
            Device.SetRenderTarget(_rtBottom); Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied_Fix);
            Batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(0, 0, RT_W / 2, RT_H), null, cRed);
            Batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(RT_W / 2, 0, RT_W - RT_W / 2, RT_H), null, cRedDark);
            Batch.End(); Device.SetRenderTarget(null);

            // ③ 中+顶（绿+蓝）RT
            Device.SetRenderTarget(_rtMidTop); Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied_Fix);
            Batch.Draw(KDefaultRes.DefaultTexture2D, green, null, cGreen);
            Batch.Draw(KDefaultRes.DefaultTexture2D, blue, null, cBlue);
            Batch.End(); Device.SetRenderTarget(null);

            // ④ 中层绿 RT
            Device.SetRenderTarget(_rtGreen); Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied_Fix);
            Batch.Draw(KDefaultRes.DefaultTexture2D, green, null, cGreen);
            Batch.End(); Device.SetRenderTarget(null);

            // ④ 顶层蓝（半透明）RT
            Device.SetRenderTarget(_rtBlue); Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied_Fix);
            Batch.Draw(KDefaultRes.DefaultTexture2D, blue, null, cBlue);
            Batch.End(); Device.SetRenderTarget(null);
        }

        private void DrawLeakDemo(SpriteBatch batch, Vector2 origin)
        {
            if (_rtDirty) { BakeAllRTs(); _rtDirty = false; } // 批次当前未激活，可安全切 RT 烘焙

            int a = AlphaLevels[_alphaIndex];
            int startX = (int)origin.X;
            int startY = (int)(ContentTop + 40);

            // 自适应：4 行在「可用高度」内尽量大，且每行之间留固定空隙（不挤、也不溢出屏幕）。
            int bottomMargin = 64;                 // 底部说明预留
            int gap = 12, arrowW = 24;
            int labelH = 20, capH = 18, interRowGap = 18;
            int availH = Device.Viewport.Height - startY - bottomMargin;
            int thumbH = (availH / 4) - (labelH + capH + interRowGap);
            thumbH = Math.Clamp(thumbH, 72, 110);  // 太小看不清，太大仍占地方
            int thumbW = (int)(thumbH * 1.5f + 0.5f); // 保持 3:2
            int rowStep = thumbH + labelH + capH + interRowGap;

            // 4 组对照，各占一行（A 无RT / B 1RT / C 2RT / D 3RT）。
            // 每行：左侧「中间产生的 RT」缩略图（没有就不显示）+ "→" + 右侧「最终合成图」（4 个最终图对齐成一列便于比较）。
            string[] labels = {
                "A　3 个 Draw 直接画（无中间 RT）",
                "B　1 张 RT（三层合一）",
                "C　2 张 RT（底 / 中上）",
                "D　3 张 RT（每层各自）",
            };
            string[][] names = {
                new string[0],
                new[] { "RT(三层合一)" },
                new[] { "RT底(最大)", "RT中上(中+顶)" },
                new[] { "RT底(最大)", "RT中(中等)", "RT顶(半透明)" },
            };
            RenderTarget2D[][] parts = {
                new RenderTarget2D[0],
                new[] { _rtAll },
                new[] { _rtBottom, _rtMidTop },
                new[] { _rtBottom, _rtGreen, _rtBlue },
            };

            int finalX = startX + 3 * (thumbW + gap) + arrowW + gap;

            // RT 缩略图与「最终图(B/C/D)」：RT 内含预乘 alpha，必须用 premultiplied（AlphaBlend）显示，
            // 否则半透明层会被二次预乘 → 颜色发暗/偏透（这正是之前 4 组不一致的根因）。
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            for (int i = 0; i < 4; i++)
            {
                int ry = startY + i * rowStep;
                int cx = startX;
                foreach (var rt in parts[i])
                {
                    batch.Draw(rt, new Rectangle(cx, ry, thumbW, thumbH), null, Color.White);
                    cx += thumbW + gap;
                }
                if (i == 1)
                    batch.Draw(_rtAll, new Rectangle(finalX, ry, thumbW, thumbH), null, Color.White);          // B 最终 = 该 RT
                else if (i == 2)
                {
                    batch.Draw(_rtBottom, new Rectangle(finalX, ry, thumbW, thumbH), null, Color.White);        // C 最终
                    batch.Draw(_rtMidTop, new Rectangle(finalX, ry, thumbW, thumbH), null, Color.White);
                }
                else if (i == 3)
                {
                    batch.Draw(_rtBottom, new Rectangle(finalX, ry, thumbW, thumbH), null, Color.White);        // D 最终
                    batch.Draw(_rtGreen, new Rectangle(finalX, ry, thumbW, thumbH), null, Color.White);
                    batch.Draw(_rtBlue, new Rectangle(finalX, ry, thumbW, thumbH), null, Color.White);
                }
                // i == 0（A）的最终图在下面用 NonPremultiplied 直接画
            }
            batch.End();

            // A 组最终图：直接 3 个 Draw（源是带 tint 的白图，非预乘），必须用 NonPremultiplied；与 B/C/D 的最终图应当一致。
            {
                int ry = startY;
                batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
                (Color caBottom, Color caMid, Color caTop) = LayerColors(a);
                DrawThreeLayers(batch, finalX, ry, thumbW, thumbH, caBottom, caMid, caTop);
                batch.End();
            }

            // 文本：行标签 + 缩略图名 + 箭头 + 最终标注 + 底部说明
            batch.Begin();
            for (int i = 0; i < 4; i++)
            {
                int ry = startY + i * rowStep;
                batch.DrawString(Font, labels[i], new Vector2(startX, ry - 18), new Color(255, 206, 110));
                int cx = startX;
                foreach (var n in names[i])
                {
                    batch.DrawString(Font, n, new Vector2(cx, ry + thumbH + 2), Color.LightGray);
                    cx += thumbW + gap;
                }
                if (i == 0)
                    batch.DrawString(Font, "（直接 3 Draw 合成，无中间 RT）", new Vector2(startX, ry + thumbH + 2), Color.LightGray);
                batch.DrawString(Font, "→", new Vector2(finalX - arrowW, ry + thumbH / 2 - 8), new Color(210, 210, 210));
                batch.DrawString(Font, "最终合成图", new Vector2(finalX, ry + thumbH + 2), new Color(150, 200, 255));
            }
            int noteY = startY + 4 * rowStep + 8;
            batch.DrawString(Font, "同样的 3 层，拆成 0/1/2/3 张 RT 渲染，最终图应当完全一致（半透明蓝层无论拆几张 RT 都一样）。",
                new Vector2(startX, noteY), new Color(150, 200, 255));
            batch.DrawString(Font, "点「半透明浓度」调 alpha；点「配色」切换三层颜色；Esc 返回。", new Vector2(startX, noteY + 22), Color.LightGray);
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
