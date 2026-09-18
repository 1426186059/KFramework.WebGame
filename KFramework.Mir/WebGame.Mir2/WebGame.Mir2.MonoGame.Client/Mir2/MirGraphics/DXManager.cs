using System;
using System.Collections.Generic;
using MirEngine;
using Client.MirControls;
using Client.MirScenes;

namespace Client.MirGraphics
{
    // 浏览器版 DXManager：Unity 风格即时模式精灵渲染器（单一画布 + 离屏 RenderTarget）。
    // 所有绘制直接走 BrowserCanvas（int 句柄：0=主画布，>0=离屏 canvas），不再依赖 Crystal 的 IRenderingPipeline 框架。
    class DXManager
    {
        public static List<MImage> TextureList = new List<MImage>();
        public static List<MirControl> ControlList = new List<MirControl>();

        public static float Opacity = 1F;
        public static bool Blending;
        public static float BlendingRate;
        public static BlendMode BlendingMode;

        public static SlimDX.Direct3D9.Sprite Sprite = new SlimDX.Direct3D9.Sprite();
        public static SlimDX.Direct3D9.Line Line = new SlimDX.Direct3D9.Line();

        // 兼容原版占位（Forms/MirScene/GameScene 仍有 DXManager.Device.Clear / new Texture(DXManager.Device,...) 等调用）。
        public static SlimDX.Direct3D9.Device Device = new SlimDX.Direct3D9.Device();
        public static SlimDX.Direct3D9.Surface CurrentSurface;

        public static bool GrayScale;

        // 光照 / 雷达 / 地板等特效资源占位（浏览器端为可选视觉，暂以空纹理承载，不阻塞编译与基本运行）。
        public static SlimDX.Direct3D9.Texture RadarTexture;
        public static List<SlimDX.Direct3D9.Texture> Lights = new List<SlimDX.Direct3D9.Texture>();
        public static SlimDX.Direct3D9.Texture PoisonDotBackground;
        public static SlimDX.Direct3D9.Texture FloorTexture, LightTexture;
        public static SlimDX.Direct3D9.Surface FloorSurface, LightSurface;
        public static Point[] LightSizes =
        {
            new Point(125,95), new Point(205,156), new Point(285,217), new Point(365,277),
            new Point(445,338), new Point(525,399), new Point(605,460), new Point(685,521),
            new Point(765,581), new Point(845,642), new Point(925,703)
        };

        public static int DPSCounter;

        public static void Create()
        {
            SlimDX.Direct3D9.Device.CurrentTarget = 0;

            // 原版 Crystal 的 SetBlend(true, rate, mode) 除 INVLIGHT 外一律是加色混合（SourceAlpha/One），
            // 与 Mir3(Zircon) 的 setBlend 语义不同；JS 侧默认用 Mir3 那张表，必须显式切到 crystal。
            // 不切换的话，安全区结界 / 点击地面这类靠"加色 + 黑底"呈现的特效会被当成 screen，
            // 画出来就是一坨不透明黑底加亮块。
            BrowserCanvas.SetBlendProfile("crystal");
        }

        static Color ToColor(SlimDX.Color4 c) =>
            Color.FromArgb((int)(c.Alpha * 255), (int)(c.Red * 255), (int)(c.Green * 255), (int)(c.Blue * 255));

        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color)
        {
            if (texture == null || !texture.Valid) return;
            Rectangle src = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            SlimDX.Vector3 pos = position ?? SlimDX.Vector3.Zero;
            SlimDX.Direct3D9.Device.ApplyTarget();
            BrowserCanvas.DrawImage(texture.Handle, src.X, src.Y, src.Width, src.Height, pos.X, pos.Y, src.Width, src.Height, ToColor(color).ToArgb());
            CMain.DPSCounter++;
        }

        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color)
        {
            if (texture == null || !texture.Valid) return;
            SlimDX.Direct3D9.Device.ApplyTarget();
            BrowserCanvas.DrawImage(texture.Handle, sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height, destRect.X, destRect.Y, destRect.Width, destRect.Height, ToColor(color).ToArgb());
            CMain.DPSCounter++;
        }

        public static void DrawOpaque(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity)
        {
            var c = color; c.Alpha = opacity; Draw(texture, sourceRect, position, c);
        }

        public static void SetSurface(SlimDX.Direct3D9.Surface surface)
        {
            SlimDX.Direct3D9.Device.CurrentTarget = surface == null ? 0 : surface.Handle;
        }

        public static void SetGrayscale(bool value) { GrayScale = value; }
        public static void SetOpacity(float opacity) { Opacity = opacity; }
        public static void SetBlend(bool value, float rate = 1F, BlendMode mode = BlendMode.NORMAL)
        {
            Blending = value; BlendingRate = rate; BlendingMode = mode;
            BrowserCanvas.SetBlendState((int)mode, rate, value);
        }
        public static void SetNormal(float blend, Color tintcolor) { }
        public static void SetGrayscale(float blend, Color tintcolor) { }
        public static void SetBlendMagic(float blend, Color tintcolor) { }

        public static void AttemptReset() { }
        public static void ResetDevice() { }
        public static void AttemptRecovery() { }

        public static SlimDX.Direct3D9.Texture CreateRenderTarget(int w, int h)
        {
            int id = BrowserCanvas.CreateOffscreen(Math.Max(1, w), Math.Max(1, h));
            return new SlimDX.Direct3D9.Texture(id, w, h);
        }

        public static void ClearControlTexture(SlimDX.Direct3D9.Texture tex, Color backColour)
        {
            if (tex == null || !tex.Valid) return;
            int prev = SlimDX.Direct3D9.Device.CurrentTarget;
            SlimDX.Direct3D9.Device.CurrentTarget = tex.Handle;
            SlimDX.Direct3D9.Device.ApplyTarget();
            BrowserCanvas.Clear(backColour);
            SlimDX.Direct3D9.Device.CurrentTarget = prev;
        }

        // 每帧渲染：清主画布 → 场景绘制 → 提交。对应原版 RenderingPipelineManager.RenderFrame。
        public static void RenderFrame(Action draw)
        {
            SlimDX.Direct3D9.Device.CurrentTarget = 0;
            SlimDX.Direct3D9.Device.ApplyTarget();
            BrowserCanvas.Clear(Color.Black);
            draw?.Invoke();
            BrowserCanvas.Flush();
        }

        public static void Clean()
        {
            for (int i = TextureList.Count - 1; i >= 0; i--)
            {
                MImage m = TextureList[i];
                if (m == null) { TextureList.RemoveAt(i); continue; }
                if (CMain.Time <= m.CleanTime) continue;
                m.DisposeTexture();
            }
            for (int i = ControlList.Count - 1; i >= 0; i--)
            {
                MirControl c = ControlList[i];
                if (c == null) { ControlList.RemoveAt(i); continue; }
                if (CMain.Time <= c.CleanTime) continue;
                c.DisposeTexture();
            }
        }

        public static void Dispose() { }
    }
}
