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

namespace Client.MirGraphics
{
    // KFramework.MonoGame 版 DXManager：主精灵路径走 GraphicsDevice / SpriteBatch（Texture2D 承载），
    // 离屏渲染目标（地板/光照烘焙）当前 KFramework.MonoGame 的 SpriteBatch 不暴露 RenderTarget，
    // 暂以无操作桩保留（DXManager.FloorTexture/LightTexture/Lights 等绘制不生效，画面地板/光照暂缺，
    // 待引擎提供 RenderTarget2D 后重写 GameScene 的烘焙路再补）。不再依赖旧 BrowserCanvas 后端。
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

        // KFramework.MonoGame 渲染后端（由 MirGame.Initialize 注入）。
        public static GraphicsDevice GDevice;
        public static SpriteBatch Batch;
        public static void Initialize(GraphicsDevice device, SpriteBatch batch)
        {
            GDevice = device;
            Batch = batch;
        }

        public static void Create()
        {
            // 旧 BrowserCanvas 的 SetBlendProfile("crystal") 已不再需要：混合语义由下方 Draw 按 Blending 标志选择。
        }

        static KFramework.MonoGame.Color ToColor(SlimDX.Color4 c) =>
            new KFramework.MonoGame.Color((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255), (byte)(c.Alpha * 255));

        static KFramework.MonoGame.Rectangle ToRect(Rectangle r) =>
            new KFramework.MonoGame.Rectangle(r.X, r.Y, r.Width, r.Height);

        // —— 主精灵路径：Texture2D（KFramework.MonoGame）——
        public static void Draw(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color)
        {
            if (texture == null) return;
            Rectangle src = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            SlimDX.Vector3 pos = position ?? SlimDX.Vector3.Zero;
            KFramework.MonoGame.BlendState blend = Blending ? KFramework.MonoGame.BlendState.Additive : KFramework.MonoGame.BlendState.NonPremultiplied;
            Batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred, blend, KFramework.MonoGame.SamplerState.PointClamp);
            Batch.Draw(texture, new KFramework.MonoGame.Vector2(pos.X, pos.Y), ToRect(src), ToColor(color));
            Batch.End();
            CMain.DPSCounter++;
        }

        public static void Draw(Texture2D texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color)
        {
            if (texture == null) return;
            KFramework.MonoGame.BlendState blend = Blending ? KFramework.MonoGame.BlendState.Additive : KFramework.MonoGame.BlendState.NonPremultiplied;
            Batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred, blend, KFramework.MonoGame.SamplerState.PointClamp);
            Batch.Draw(texture,
                new KFramework.MonoGame.Rectangle((int)destRect.X, (int)destRect.Y, (int)destRect.Width, (int)destRect.Height),
                ToRect(sourceRect), ToColor(color));
            Batch.End();
            CMain.DPSCounter++;
        }

        public static void DrawOpaque(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity)
        {
            var c = color; c.Alpha = opacity; Draw(texture, sourceRect, position, c);
        }

        // —— 渲染目标路径（离屏烘焙，KFramework 暂不支持）：无操作，地板/光照不绘制 ——
        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color) { }
        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color) { }
        public static void DrawOpaque(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity) { }

        public static void SetSurface(SlimDX.Direct3D9.Surface surface) { }   // 离屏渲染目标暂不支持
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

        // 离屏渲染目标（KFramework.MonoGame 的 SpriteBatch 不暴露 RenderTarget）：返回无效占位纹理，
        // 调用方（GameScene 烘焙）据此跳过真实离屏合成，地板/光照暂不显示。
        public static SlimDX.Direct3D9.Texture CreateRenderTarget(int w, int h)
        {
            return new SlimDX.Direct3D9.Texture(-1, w, h);
        }

        public static void ClearControlTexture(SlimDX.Direct3D9.Texture tex, Color backColour) { }

        // 每帧渲染：清主画布 → 场景绘制（各 DXManager.Draw 内部自管 Begin/End）→ 提交。
        public static void RenderFrame(Action draw)
        {
            GDevice.Clear(KFramework.MonoGame.Color.Black);
            draw?.Invoke();
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
