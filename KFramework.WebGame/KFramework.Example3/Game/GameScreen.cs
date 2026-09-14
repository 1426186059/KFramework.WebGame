
namespace KFramework.Example3
{
    internal class GameScreen : KUIBase
    {
        private ContentManager contentManager;

        private KLabel mLabel_Name_Score;
        private KImage mLabel_Name_CoinCount;
        private KLabel mLabel_Name_Time;

        private KLabel mLabel_Score;
        private KLabel mLabel_CoinCount;
        private KLabel mLabel_Time;

        ContentManager mContentManager;
        public GameScreen(Level mLevel)
        {
            mContentManager = KSceneMgr.Game.Content;

            Parent = KUIRoot.Instance.GetCanvas(0);
            Pivot = Vector2.One * 0.5f;
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
            AnchorOffset = KRectangleFOffset.Zero;

            mLabel_Name_Score = new KLabel()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(-200, -250),
                Text = Resources.分数,
            };

            mLabel_Name_CoinCount = new KImage()
            {
                Parent = this,
                UseNativeSize = true,
                Sprite = new KSprite(mLevel.mSpriteSheet_misc3Atlas.Sprite("misc-3_83")),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(0, -250),
                LocalScale = Vector2.One * 2
            };

            mLabel_Name_Time = new KLabel()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(200, -250),
                Text = Resources.时间,
            };

            mLabel_Score = new KLabel()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(-200, -220),
                Text = "00",
            };

            mLabel_CoinCount = new KLabel()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(0, -220),
                Text = "00"
            };

            mLabel_Time = new KLabel()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(200, -220),
                Text = "00:00",
            };
        }

        public override void Update()
        {
            base.Update();
            mLabel_CoinCount.Text = PlayerData.Instance.nJinBiCount.ToString();
            mLabel_Score.Text = PlayerData.Instance.nScore.ToString();
            mLabel_Time.Text = TimeTool.GetFormatStringByTimeSpan(PlayerData.Instance.nTime);
        }

        public override void Draw()
        {
            base.Draw();
        }

    }
}
