namespace KFramework.Example3
{
    internal class TestScreen : KUIBase
    {
        private ContentManager contentManager;

        private KButton LeftTopBtn;
        private KButton LeftBtn;
        private KButton LeftBottomBtn;
        private KButton MiddleTopBtn;
        private KButton MiddleBtn;
        private KButton MiddleBottomBtn;
        private KButton RightTopBtn;
        private KButton RightBtn;
        private KButton RightBottomBtn;

        public TestScreen()
        {
            contentManager = KSceneMgr.Game.Content;

            Parent = KUIRoot.Instance.GetCanvas(0);
            AnchorPosition = Vector2.Zero;
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
            AnchorOffset = KRectangleFOffset.Zero;

            LeftTopBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = Vector2.Zero,
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("LeftTopBtn", Color.Red)
            };

            LeftBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(0, 0.5f),
                Pivot = new Vector2(0, 0.5f),
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("LeftBtn", Color.Blue)
            };

            LeftBottomBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(0, 1f),
                Pivot = new Vector2(0, 1),
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("LeftBottomBtn", Color.Blue)
            };

            MiddleTopBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(0.5f, 0),
                Pivot = new Vector2(0.5f, 0f),
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("MiddleTopBtn", Color.Blue)
            };

            MiddleBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(0.5f, 0.5f),
                Pivot = Vector2.One * 0.5f,
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("MiddleBtn", Color.Blue)
            };

            MiddleBottomBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(0.5f, 1f),
                Pivot = new Vector2(0.5f, 1f),
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("MiddleBottomBtn", Color.Blue)
            };

            RightTopBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(1.0f, 0f),
                Pivot = new Vector2(1, 0f),
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("RightTopBtn")
            };

            RightBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(1.0f, 0.5f),
                Pivot = new Vector2(1, 0.5f),
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("RightBtn")
            };

            RightBottomBtn = new KButton()
            {
                Parent = this,
                Size = new Vector2(200, 50),
                Anchor = new Vector2(1.0f, 1f),
                Pivot = new Vector2(1, 1),
                AnchorPosition = new Vector2(0, 0),
                Label = new KLabel("RightBottomBtn")
            };

            // 按钮已在构造时自动注册到输入系统，这里只需挂回调，用法同 Unity
            MiddleBtn.PointerClickEvent += (s, e) => OnMiddleBtnClick();

            // 演示不可交互状态：变灰且不响应点击
            //RightBottomBtn.Interactable = false;
        }

        private void OnMiddleBtnClick()
        {
            PrintTool.Log("OnMiddleBtnClick");
            MiddleBtn.Label.Text = "Clicked!";
        }

    }
}
