namespace KFramework.Example3
{
    internal class StartScreen : KUIBase
    {
        private ContentManager contentManager;

        private KButton StartBtn;
        private KButton SetttingBtn;
        private KButton NewGameBtn;

        private KLabel mLabel_Name_Score;
        private KImage mLabel_Name_CoinCount;
        private KLabel mLabel_Name_Time;

        private KLabel mLabel_Score;
        private KLabel mLabel_CoinCount;
        private KLabel mLabel_Time;

        ContentManager mContentManager;
        public StartScreen(Level mLevel)
        {
            mContentManager = KSceneMgr.Game.Content;

            Parent = KUIRoot.Instance.GetCanvas(0);
            Pivot = Vector2.One * 0.5f;
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
            AnchorOffset = KRectangleFOffset.Zero;

            new KImage() //灰色背景
            {
                Parent = this,
                MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1),
                AnchorOffset = KRectangleFOffset.Zero,
                Sprite = KDefaultRes.DefaultSprite,
                Color = new Color(Color.Gray.R, Color.Gray.G, Color.Gray.B, (byte)150)
            };

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


            StartBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(150, 30),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(0, -50),
                Label = new KLabel(Resources.开始游戏)
            };

            SetttingBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(150, 30),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel(Resources.设置)
            };

            NewGameBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(150, 30),
                Pivot = Vector2.One * 0.5f,
                Anchor = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(0, 50),
                Label = new KLabel(Resources.新游戏)
            };

            // 按钮已在构造时自动注册到输入系统，这里只需挂回调，用法同 Unity
            StartBtn.PointerClickEvent += (s, e) => OnClick_Btn(s, e);
            SetttingBtn.PointerClickEvent += (s, e) => OnClick_Btn(s, e);
            NewGameBtn.PointerClickEvent += (s, e) => OnClick_Btn(s, e);
        }

        private void OnClick_Btn(object sender, KPointerEventArgs arg)
        {
            PrintTool.Log("OnClick_Btn: " + (sender as KButton).Name);
            if (sender == StartBtn)
            {
                (KSceneMgr.Main as MainScene).LoadNextLevel();
                this.Dispose();
            }
            else if (sender == SetttingBtn)
            {

            }
            else if (sender == NewGameBtn)
            {

            }

        }

        public override void Draw()
        {
            base.Draw();
        }

    }
}
