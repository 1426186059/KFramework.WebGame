using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 调试用渲染统计浮层。
    /// <para>
    /// 默认只显示 FPS；按 <b>F12</b> 展开显示完整的 <see cref="GraphicsMetrics"/>
    /// （计数由 <see cref="GraphicsDevice.Metrics"/> 提供，整帧累计、帧末重置）。
    /// </para>
    /// <para>
    /// 注意：浏览器里 F12 通常被"打开开发者工具"占用，页面可能收不到该按键 ——
    /// 若按 F12 没反应，先点一下画布把焦点拿回来再按，或改用其它按键。
    /// </para>
    /// </summary>
    public class MetricsScreen : KWidget, KDrawable
    {
        /// <summary>只显示 FPS 时背景条的宽度。</summary>
        private const float NarrowWidth = 120f;

        /// <summary>展开时的背景条宽度 / 高度（容下 FPS 行 + 两行计数）。</summary>
        private const float FullWidth = 560f;
        private const float FullHeight = 86f;

        KImage mImage;
        KLabel mLable;

        int nFPS = 0;

        /// <summary>是否展开显示完整统计（F12 切换）。</summary>
        bool mShowFull = false;

        /// <summary>
        /// 统计快照。<see cref="GraphicsMetrics"/> 是值类型，这里存的是<b>上一帧末采样</b>的那一份
        /// （采样在 Draw 阶段、显示在下一个 Update 阶段，避免同一帧里读到自己造成的观感错位）。
        /// </summary>
        GraphicsMetrics mMetrics;

        public MetricsScreen()
        {
            Parent = KSceneMgr.DontDestroyOnLoadRoot;
            Pivot = Vector2.One * 0.5f;
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
            AnchorOffset = KRectangleFOffset.Zero;

            mImage = new KImage()
            {
                Parent = this,
                Size = new Vector2(NarrowWidth, 30),
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
            // 用墙钟统计的真实帧率：1 / KTime.deltaTime 在固定步长下恒为 60（deltaTime 是逻辑步长）
            nFPS = (int)Math.Round(KTime.realFps);

            // F12 在"只显示 FPS"与"展开全部 GraphicsMetrics"之间切换。
            if (Input_KeyBoard.GetKeyDown(Keys.Tab))
            {
                mShowFull = !mShowFull;
            }

            // 展开时：第一行 FPS + 视口，后两行交给 GraphicsMetrics.ToString()（内部用 '\n' 分行）。
            mLable.Text = mShowFull
                ? $"FPS: {nFPS}   Screen: {KSceneMgr.Game.GraphicsDevice.Viewport.Bounds.Size}\n{mMetrics}"
                : $"FPS: {nFPS}";

            // 背景条跟着展开状态换尺寸（只在真的变了时赋值，免得每帧触发无谓的重建）。
            Vector2 want = mShowFull ? new Vector2(FullWidth, FullHeight) : new Vector2(NarrowWidth, 30);
            if (mImage.Size != want) mImage.Size = want;
        }

        public void DrawMe()
        {
            // 照 MonoGame：GraphicsDevice.Metrics 是整帧累计的渲染统计（每帧末 EndFrame 重置一次）。
            mMetrics = KSceneMgr.Game.GraphicsDevice.Metrics;
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
