using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    public class MetricsScreen : KWidget, KDrawable
    {
        KImage mImage;
        KLabel mLable;
        KLabel mLable2;

        int nFPS = 0;
        long nClearCount = 0;
        long nDrawCount = 0;
        long nPrimitiveCount = 0;
        long nSpriteCount = 0;
        long nTextureCount = 0;
        long nTargetCount = 0;
        long nVertexShaderCount = 0;
        long nPixelShaderCount = 0;

        public MetricsScreen()
        {
            Parent = KSceneMgr.DontDestroyOnLoadRoot;
            Pivot = Vector2.One * 0.5f;
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
            AnchorOffset = KRectangleFOffset.Zero;

            // 背景条要罩住两行文本（第二行较长，按 20px 字号预留 ~660 宽）。
            mImage = new KImage()
            {
                Parent = this,
                Size = new Vector2(660, 56),
                Anchor = Vector2.Zero,
                Pivot = Vector2.Zero,
                AnchorPosition = new Vector2(0, 0),
                Sprite = KDefaultRes.DefaultSprite,
                Color = new Color(Color.Black.R, Color.Black.G, Color.Black.B, (byte)150)
            };

            mLable = new KLabel()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = Vector2.Zero,
                Pivot = Vector2.Zero,
                AnchorPosition = new Vector2(0, 0),
            };

            // 第二行：渲染目标 / 纹理 / 着色器 等"切换次数"计数（第一行放 FPS / DC / Sprites）。
            mLable2 = new KLabel()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = Vector2.Zero,
                Pivot = Vector2.Zero,
                AnchorPosition = new Vector2(0, 22),
            };
        }

        public override void Update()
        {
            base.Update();
            // 用墙钟统计的真实帧率：1 / KTime.deltaTime 在固定步长下恒为 60（deltaTime 是逻辑步长）
            nFPS = (int)Math.Round(KTime.realFps);
            mLable.Text = $"FPS: {nFPS}  DC: {nDrawCount}  Sprites: {nSpriteCount}  ScreenSize: {KSceneMgr.Game.GraphicsDevice.Viewport.Bounds.Size}";
            mLable2.Text = $"Prim: {nPrimitiveCount}  Tex: {nTextureCount}  Target: {nTargetCount}  VS: {nVertexShaderCount}  PS: {nPixelShaderCount}  Clear: {nClearCount}";
        }

        public void DrawMe()
        {
            // 照 MonoGame：GraphicsDevice.Metrics 是整帧累计的渲染统计（每帧末 EndFrame 重置一次）。
            var metrics = KSceneMgr.Game.GraphicsDevice.Metrics;
            nClearCount = metrics.ClearCount;
            nDrawCount = metrics.DrawCount;
            nPrimitiveCount = metrics.PrimitiveCount;
            nSpriteCount = metrics.SpriteCount;
            nTextureCount = metrics.TextureCount;
            nTargetCount = metrics.TargetCount;
            nVertexShaderCount = metrics.VertexShaderCount;
            nPixelShaderCount = metrics.PixelShaderCount;
            mImage.Draw();
            mLable.Draw();
            mLable2.Draw();
        }

        public override void Draw()
        {
            var mSpriteBatch = KSceneMgr.SpriteBatch;
            mSpriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                samplerState: SamplerState.PointClamp,
                blendState: BlendState.NonPremultiplied);

            DrawMe();
            mSpriteBatch.End();
        }
    }
}
