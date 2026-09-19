using System;
using Client.MirControls;
using Client.MirScenes;
using KFramework.MonoGame;
using MirEngine;
using SlimDX;
using SlimDX.Direct3D9;

// 让 Point/Color/Rectangle 默认指向 MirEngine（游戏逻辑层使用的类型）；KFramework.MonoGame 的同名类型一律全限定。
using Point = MirEngine.Point;
using Color = MirEngine.Color;
using Rectangle = MirEngine.Rectangle;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirGraphics
{
    // KFramework.MonoGame 版 DXManager：主精灵路径走 GraphicsDevice / SpriteBatch（Texture2D 承载），
    // 离屏渲染目标由 KFramework.MonoGame.RenderTarget2D（真·FBO）承载——CreateRenderTarget / SetSurface /
    // DrawOpaque(ControlTexture) 现在都会真正生效，GameScene 的地板 / 光照烘焙可正常合成。
    // 控制 ControlTexture 的 SlimDX 壳（Shims/SlimDX.cs 的 Texture/Surface）负责把调用转到底层 RenderTarget2D。
    class DXManager
    {
        public static List<MImage> TextureList = new List<MImage>();
        public static List<MirControl> ControlList = new List<MirControl>();

        public static float Opacity = 1F;

        // 全屏自适应：场景烘焙时注入的全局缩放变换（按屏幕高度统一缩放，参考 Unity 的
        // Scale-With-Screen-Size / Match=Height）。仅在 CreateTexture 烘焙期间置位，
        // PresentToScreen 上屏前必须清空，否则会把整帧再缩放一次（双重缩放/错位）。
        public static KFramework.MonoGame.Matrix4x4? RenderTransform = null;

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

        // KFramework.MonoGame 渲染后端（由 MirGame.Initialize 注入）。
        public static GraphicsDevice GDevice;
        public static SpriteBatch Batch;
        public static void Initialize(GraphicsDevice device, SpriteBatch batch)
        {
            GDevice = device;
            Batch = batch;

            // 嵌套 RT 合成：切回画布（SetRenderTarget(null)）时保留已合成内容，避免闪烁。
            // 清屏仍由每帧 RenderFrame 的显式 Clear 负责；默认 DiscardContents 保持 XNA/MonoGame 兼容。
            GDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
        }

        public static void Create()
        {
            // 旧 BrowserCanvas 的 SetBlendProfile("crystal") 已不再需要：混合语义由下方 Draw 按 Blending 标志选择。
        }

        static KFramework.MonoGame.Color ToColor(SlimDX.Color4 c) =>
            new KFramework.MonoGame.Color((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255), (byte)(c.Alpha * 255));

        static KFramework.MonoGame.Color ToColor(Color c) =>
            new KFramework.MonoGame.Color(c.R, c.G, c.B, c.A);

        static KFramework.MonoGame.Rectangle ToRect(Rectangle r) =>
            new KFramework.MonoGame.Rectangle(r.X, r.Y, r.Width, r.Height);

        // —— 主精灵路径：Texture2D（KFramework.MonoGame）——
        // 关键：Batch.Begin/End 必须用 try/finally 配对。否则某次 Batch.Draw 抛异常（如坏贴图、
        // 非法源矩形）会导致 SpriteBatch 卡在"已 Begin"状态，下一帧首个 Begin 再抛
        // InvalidOperationException 被 CMain.Loop 吞掉 -> 每帧只清黑屏（永久黑屏，见 MLibrary 注释）。
        public static void Draw(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color)
        {
            if (texture == null) return;
            Rectangle src = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            SlimDX.Vector3 pos = position ?? SlimDX.Vector3.Zero;
            KFramework.MonoGame.BlendState blend = Blending ? KFramework.MonoGame.BlendState.Additive : KFramework.MonoGame.BlendState.NonPremultiplied;
            Batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred, blend, KFramework.MonoGame.SamplerState.PointClamp, RenderTransform ?? KFramework.MonoGame.Matrix4x4.Identity);
            try
            {
                Batch.Draw(texture, new KFramework.MonoGame.Vector2(pos.X, pos.Y), ToRect(src), ToColor(color));
                CMain.DPSCounter++;
            }
            finally
            {
                Batch.End();
            }
        }

        public static void Draw(Texture2D texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color)
        {
            if (texture == null) return;
            KFramework.MonoGame.BlendState blend = Blending ? KFramework.MonoGame.BlendState.Additive : KFramework.MonoGame.BlendState.NonPremultiplied;
            Batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred, blend, KFramework.MonoGame.SamplerState.PointClamp, RenderTransform ?? KFramework.MonoGame.Matrix4x4.Identity);
            try
            {
                Batch.Draw(texture,
                    new KFramework.MonoGame.Rectangle((int)destRect.X, (int)destRect.Y, (int)destRect.Width, (int)destRect.Height),
                    ToRect(sourceRect), ToColor(color));
                CMain.DPSCounter++;
            }
            finally
            {
                Batch.End();
            }
        }

        public static void DrawOpaque(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity)
        {
            var c = color; c.Alpha = opacity; Draw(texture, sourceRect, position, c);
        }

        // —— 离屏渲染目标路径：ControlTexture 是包裹了 KFramework.MonoGame.RenderTarget2D 的 SlimDX 纹理，
        //    烘焙完成后当作普通 Texture2D 采样绘制（照 MonoGame 把 RenderTarget2D 当纹理用）。
        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color)
        {
            if (texture?.RenderTarget == null) return;
            Draw(texture.RenderTarget, sourceRect, position, color);
        }

        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color)
        {
            if (texture?.RenderTarget == null) return;
            Draw(texture.RenderTarget, sourceRect, destRect, color);
        }

        public static void DrawOpaque(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity)
        {
            var c = color; c.Alpha = opacity;
            Draw(texture, sourceRect, position, c);
        }

        // 离屏渲染目标：把 surface 所属的 RenderTarget2D 绑到设备（null = 回默认画布）。
        // 嵌套合成时按"保存/恢复 CurrentSurface"的方式调用，保证子控件烘焙后能回到父 RT。
        public static void SetSurface(SlimDX.Direct3D9.Surface surface)
        {
            CurrentSurface = surface;
            GDevice?.SetRenderTarget(surface?.Owner?.RenderTarget);
        }
        public static void SetGrayscale(bool value) { GrayScale = value; }
        public static void SetOpacity(float opacity) { Opacity = opacity; }
        public static void SetBlend(bool value, float rate = 1F, BlendMode mode = BlendMode.NORMAL)
        {
            Blending = value; BlendingRate = rate; BlendingMode = mode;
        }
        public static void SetNormal(float blend, Color tintcolor) { }
        public static void SetGrayscale(float blend, Color tintcolor) { }
        public static void SetBlendMagic(float blend, Color tintcolor) { }

        public static void AttemptReset() { }
        public static void ResetDevice() { }
        public static void AttemptRecovery() { }

        // 离屏渲染目标：走 KFramework.MonoGame.RenderTarget2D（SlimDX 壳的 Texture 构造里创建）。
        public static SlimDX.Direct3D9.Texture CreateRenderTarget(int w, int h)
        {
            return new SlimDX.Direct3D9.Texture(DXManager.Device, w, h, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
        }

        public static void ClearControlTexture(SlimDX.Direct3D9.Texture tex, Color backColour)
        {
            if (tex?.RenderTarget == null) return;
            // 绑到该离屏目标清屏，随后恢复到"当前渲染目标"——这是嵌套合成的关键：
            // 场景烘焙子控件时，子控件清屏后必须回到场景 RT，而非默认画布。
            var saved = CurrentSurface;
            SetSurface(new SlimDX.Direct3D9.Surface(tex));
            GDevice?.Clear(ToColor(backColour));
            SetSurface(saved);
        }

        // 每帧渲染：清主画布 → 场景绘制（各 DXManager.Draw 内部自管 Begin/End）→ 提交。
        public static void RenderFrame(Action draw)
        {
            GDevice.Clear(KFramework.MonoGame.Color.Black);
            draw?.Invoke();
        }

        // 浏览器端全屏呈现：场景（含所有子控件）已在固定逻辑分辨率(Settings.ScreenWidth/Height)
        // 下烘焙进离屏纹理(ControlTexture)，这里把它拉伸铺满整个画布(Viewport)。
        // 这样 UI 只需按设计分辨率布局一次即可等比居中铺满窗口，且不会只显示在左上角、
        // 四周露出 MirScene 的洋红(Magenta)底色。
        // 参数用 KFramework.MonoGame 的纹理类型（RenderTarget2D : Texture2D），
        // 避免与 SlimDX.Direct3D9.Texture 产生"不明确的引用"。
        public static void PresentToScreen(KFramework.MonoGame.Texture2D texture)
        {
            if (texture == null) return;
            RenderTransform = null;
            var vp = GDevice.Viewport;
            Draw(texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
                new RectangleF(0, 0, texture.Width, texture.Height),
                Color.White);
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
