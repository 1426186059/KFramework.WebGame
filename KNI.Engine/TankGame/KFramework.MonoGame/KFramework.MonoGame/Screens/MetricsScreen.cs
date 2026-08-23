using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace KFramework.MonoGame
{
    public class MetricsScreen : KWidget, KDrawable
    {
        KImage mImage;
        KLabel mLable;

        int nFPS = 0;
        long nDrawCount = 0;

        public MetricsScreen()
        {
            Parent = KSceneMgr.DontDestroyOnLoadRoot;
            Pivot = Vector2.One * 0.5f;
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
            AnchorOffset = KRectangleFOffset.Zero;

            mImage = new KImage()
            {
                Parent = this,
                Size = new Vector2(250, 30),
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
        }

        public override void Update()
        {
            base.Update();
            nFPS = (int)Math.Ceiling(1 / KTime.deltaTime);
            mLable.Text = $"FPS: {nFPS} DC: {nDrawCount} ScreenSize: {KSceneMgr.Game.GraphicsDevice.Viewport.Bounds.Size}";
        }

        public void DrawMe()
        {
            nDrawCount = KSceneMgr.Game.GraphicsDevice.Metrics.DrawCount;
            mImage.Draw();
            mLable.Draw();
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
