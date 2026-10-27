using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.UITest
{
    /// <summary>
    /// UI 组件总览：每种 UI 控件一个标签页，顶部按钮切换。
    /// <para>组件都挂在 KUIRoot 的 KCanvas 上（由 KSceneMgr 每帧自动 Update/Draw，显示在正文之上），
    /// 本场景只负责画页签、说明与布局；切页即切换各页根的 activeSelf，并相应开关交互（KButton.Interactable / 注销隐藏的 IClickable）。</para>
    /// </summary>
    public sealed class UITestScene : TestSceneBase
    {
        public override string Title => "UI 组件总览（标签页切换：KImage/KButton/KLabel/KSimpleLabel/KScrollView/KDropDown/KInputBox/KProgressBar）";

        private enum Page { KImage, KButton, KLabel, KSimpleLabel, KScrollView, KDropDown, KInputBox, KProgressBar }
        private Page _page = Page.KImage;

        /// <summary>用高层号，避免与引擎/示例自己的 Canvas 撞车。</summary>
        private const int CanvasLayer = 10;
        private KCanvas _canvas;

        private KTransform _pageImage, _pageButton, _pageLabel, _pageSimple, _pageScroll, _pageDrop, _pageInput, _pageProgress;

        // ── KImage 页 ──
        private KImage _imgBig, _imgTint, _imgMidPivot;

        // ── KButton 页（九个按钮钉在固定矩形九处）──
        private KImage _buttonDemoRect;
        private readonly KButton[] _anchorButtons = new KButton[9];
        private int _clickCount;

        // ── KLabel 页 ──
        private KImage _labelDemoRect;
        private readonly KLabel[] _anchorLabels = new KLabel[9];
        private KLabel _wrapLabel;

        // ── KSimpleLabel 页 ──
        private readonly KSimpleLabel[] _pivotLabels = new KSimpleLabel[9];

        // ── KScrollView 页 ──
        private KScrollView _scroll;

        // ── KDropDown 页 ──
        private KDropDown _dd;
        private KLabel _ddResult;

        // ── KInputBox 页 ──
        private KInputBox _input;
        private KLabel _inputEcho;

        // ── KProgressBar 页 ──
        private KProgressBar _bar;
        private float _barT;

        private static readonly Vector2[] NineAnchors =
        [
            new(0f, 0f), new(0.5f, 0f), new(1f, 0f),
            new(0f, 0.5f), new(0.5f, 0.5f), new(1f, 0.5f),
            new(0f, 1f), new(0.5f, 1f), new(1f, 1f),
        ];

        private IFont _font;

        public override void LoadContent()
        {
            _font = KDefaultRes.DefaultSpriteFont;
            _canvas = KUIRoot.Instance.GetCanvas(CanvasLayer);

            _pageImage = AddRoot(true);
            _pageButton = AddRoot(false);
            _pageLabel = AddRoot(false);
            _pageSimple = AddRoot(false);
            _pageScroll = AddRoot(false);
            _pageDrop = AddRoot(false);
            _pageInput = AddRoot(false);
            _pageProgress = AddRoot(false);

            BuildImagePage();
            BuildButtonPage();
            BuildLabelPage();
            BuildSimpleLabelPage();
            BuildScrollPage();
            BuildDropDownPage();
            BuildInputPage();
            BuildProgressPage();
        }

        private KTransform AddRoot(bool active)
        {
            return new KTransform { Parent = _canvas, activeSelf = active };
        }

        // ── KImage ──────────────────────────────────────────────────────────
        private void BuildImagePage()
        {
            _imgBig = new KImage { Parent = _pageImage, Color = new Color(70, 130, 200) };
            _imgTint = new KImage { Parent = _pageImage, Color = new Color(230, 150, 90) };
            _imgMidPivot = new KImage { Parent = _pageImage, Color = new Color(140, 200, 140), Pivot = Vector2.One * 0.5f };
        }

        // ── KButton ─────────────────────────────────────────────────────────
        private void BuildButtonPage()
        {
            _buttonDemoRect = new KImage { Parent = _pageButton, Color = new Color(26, 34, 52) };
            for (int i = 0; i < 9; i++)
            {
                Vector2 anchor = NineAnchors[i];
                Color bg = ButtonColor(i);
                int index = i;
                var button = new KButton
                {
                    Parent = _buttonDemoRect,
                    Size = new Vector2(160, 44),
                    Anchor = anchor,
                    Pivot = anchor,
                    AnchorPosition = Vector2.Zero,
                    Color = bg,
                    NormalColor = bg,
                    HighlightedColor = Lighten(bg, 40),
                    PressedColor = Darken(bg, 40),
                    Label = new KLabel(AnchorNames[i], Color.White, _font) { Alignment = TextAnchor.MiddleCenter },
                };
                button.PointerClickEvent += (_, _) =>
                {
                    _clickCount++;
                    button.Label.Text = $"{AnchorNames[index]} ×{_clickCount}";
                };
                _anchorButtons[i] = button;
            }
        }

        // ── KLabel ──────────────────────────────────────────────────────────
        private void BuildLabelPage()
        {
            _labelDemoRect = new KImage { Parent = _pageLabel, Color = new Color(26, 34, 52) };
            for (int i = 0; i < 9; i++)
            {
                _anchorLabels[i] = new KLabel(AnchorNames[i], ButtonColor(i), _font)
                {
                    Parent = _labelDemoRect,
                    Size = new Vector2(180, 40),
                    Anchor = NineAnchors[i],
                    Pivot = NineAnchors[i],
                    AnchorPosition = Vector2.Zero,
                    Alignment = TextAnchor.MiddleCenter,
                };
            }
            _wrapLabel = new KLabel(
                "KLabel.HorizontalWrap = Wrap：超过控件宽度就在这里折行（内部走 TextRenderer 的折行）。",
                new Color(200, 230, 255), _font)
            {
                Parent = _pageLabel,
                Alignment = TextAnchor.UpperLeft,
                HorizontalWrap = HorizontalWrapMode.Wrap,
            };
        }

        // ── KSimpleLabel ────────────────────────────────────────────────────
        private void BuildSimpleLabelPage()
        {
            for (int i = 0; i < 9; i++)
            {
                _pivotLabels[i] = new KSimpleLabel(PivotNames[i], new Color(255, 214, 150), _font)
                {
                    Parent = _pageSimple,
                    Pivot = NineAnchors[i],
                };
            }
        }

        // ── KScrollView ─────────────────────────────────────────────────────
        private void BuildScrollPage()
        {
            _scroll = new KScrollView { Parent = _pageScroll, Size = new Vector2(320, 320) };
            for (int i = 0; i < 14; i++)
            {
                int row = i;
                var item = new KImage
                {
                    Parent = _scroll.Content,
                    Size = new Vector2(280, 30),
                    Anchor = Vector2.Zero,
                    Pivot = Vector2.Zero,
                    AnchorPosition = Vector2.Zero,
                    WorldPosition = new Vector2(10, 8 + row * 36),
                    Color = ButtonColor(i % 9),
                };
            }
        }

        // ── KDropDown ───────────────────────────────────────────────────────
        private void BuildDropDownPage()
        {
            _dd = new KDropDown { Parent = _pageDrop, Size = new Vector2(220, 32) };
            _dd.Items.Add("选项 A");
            _dd.Items.Add("选项 B");
            _dd.Items.Add("选项 C");
            _dd.Items.Add("选项 D");
            _ddResult = new KLabel("未选择", new Color(200, 230, 255), _font)
            {
                Parent = _pageDrop,
                Size = new Vector2(220, 32),
                Anchor = new Vector2(0, 1),
                Pivot = new Vector2(0, 0),
                AnchorPosition = Vector2.Zero,
                Alignment = TextAnchor.UpperLeft,
            };
            _dd.OnValueChanged += (_, i) => _ddResult.Text = "已选：" + _dd.Items[i];
        }

        // ── KInputBox ────────────────────────────────────────────────────────
        private void BuildInputPage()
        {
            _input = new KInputBox { Parent = _pageInput, Size = new Vector2(280, 32) };
            _input.Placeholder = "点此输入，回车提交…";
            _inputEcho = new KLabel("提交内容：", new Color(200, 230, 255), _font)
            {
                Parent = _pageInput,
                Size = new Vector2(280, 32),
                Anchor = new Vector2(0, 1),
                Pivot = new Vector2(0, 0),
                AnchorPosition = Vector2.Zero,
                Alignment = TextAnchor.UpperLeft,
            };
            _input.OnSubmit += (_, t) => _inputEcho.Text = "提交内容：" + t;
            _input.OnTextChanged += (_, t) => { if (string.IsNullOrEmpty(t)) _inputEcho.Text = "提交内容："; };
        }

        // ── KProgressBar ────────────────────────────────────────────────────
        private void BuildProgressPage()
        {
            _bar = new KProgressBar { Parent = _pageProgress, Size = new Vector2(320, 24) };
            _bar.ShowText = true;
        }

        // ── 布局 ─────────────────────────────────────────────────────────────
        private void Layout()
        {
            BeginButtons(68);
            AddUiButton("① KImage", () => SetPage(Page.KImage), _page == Page.KImage);
            AddUiButton("② KButton", () => SetPage(Page.KButton), _page == Page.KButton);
            AddUiButton("③ KLabel", () => SetPage(Page.KLabel), _page == Page.KLabel);
            AddUiButton("④ KSimpleLabel", () => SetPage(Page.KSimpleLabel), _page == Page.KSimpleLabel);
            AddUiButton("⑤ KScrollView", () => SetPage(Page.KScrollView), _page == Page.KScrollView);
            AddUiButton("⑥ KDropDown", () => SetPage(Page.KDropDown), _page == Page.KDropDown);
            AddUiButton("⑦ KInputBox", () => SetPage(Page.KInputBox), _page == Page.KInputBox);
            AddUiButton("⑧ KProgressBar", () => SetPage(Page.KProgressBar), _page == Page.KProgressBar);

            float top = ContentTop + 24f;
            float left = 40f;
            float rectW = Math.Min(Device.Viewport.Width - left * 2f, 960f);

            switch (_page)
            {
                case Page.KImage: LayoutImages(left, top); break;
                case Page.KButton: Place(_buttonDemoRect, left, top, rectW, 340f); break;
                case Page.KLabel:
                    Place(_labelDemoRect, left, top, rectW, 300f);
                    Place(_wrapLabel, left, top + 300f + 20f, rectW, 86f);
                    break;
                case Page.KSimpleLabel: LayoutSimpleLabels(left, top, rectW); break;
                case Page.KScrollView: Place(_scroll, left, top, 320f, 320f); break;
                case Page.KDropDown:
                    Place(_dd, left, top, 220f, 32f);
                    Place(_ddResult, left, top + 44f, 220f, 32f);
                    break;
                case Page.KInputBox:
                    Place(_input, left, top, 280f, 32f);
                    Place(_inputEcho, left, top + 44f, 280f, 32f);
                    break;
                case Page.KProgressBar: Place(_bar, left, top, 320f, 24f); break;
            }
        }

        private static void Place(KWidget widget, float x, float y, float w, float h)
        {
            widget.Size = new Vector2(w, h);
            widget.WorldPosition = new Vector2(x, y);
        }

        private void LayoutImages(float x, float y)
        {
            Place(_imgBig, x, y, 240f, 120f);
            Place(_imgTint, x + 270f, y, 120f, 120f);
            Place(_imgMidPivot, x + 440f + 60f, y + 60f, 120f, 120f);
        }

        private void LayoutSimpleLabels(float x, float y, float width)
        {
            float demoH = 220f;
            for (int i = 0; i < 9; i++)
            {
                var point = new Vector2(x + NineAnchors[i].X * width, y + NineAnchors[i].Y * demoH);
                _pivotLabels[i].WorldPosition = point;
            }
        }

        private void SetPage(Page page)
        {
            _page = page;
            _pageImage.activeSelf = page == Page.KImage;
            _pageButton.activeSelf = page == Page.KButton;
            _pageLabel.activeSelf = page == Page.KLabel;
            _pageSimple.activeSelf = page == Page.KSimpleLabel;
            _pageScroll.activeSelf = page == Page.KScrollView;
            _pageDrop.activeSelf = page == Page.KDropDown;
            _pageInput.activeSelf = page == Page.KInputBox;
            _pageProgress.activeSelf = page == Page.KProgressBar;

            // KButton 注册在全局输入上，仅靠 activeSelf 挡不住命中 —— 一并关掉非当前页的交互。
            bool btnLive = page == Page.KButton;
            foreach (KButton b in _anchorButtons) b.Interactable = btnLive;
            _dd.Interactable = page == Page.KDropDown;

            // 隐藏页的输入框/滚动视图：注销其 IClickable，避免不显示时仍吃点击或键盘。
            if (page == Page.KInputBox) _input.Register(); else _input.Unregister();
            if (page == Page.KScrollView) KInputMgr.Register(_scroll); else KInputMgr.Unregister(_scroll);
        }

        public override void Update()
        {
            base.Update();
            Layout();

            if (_page == Page.KProgressBar)
            {
                _barT += KTime.deltaTime * 1.5f;
                _bar.Progress = 0.5f + 0.5f * (float)System.Math.Sin(_barT);
            }

            ClickButtons();
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            Layout();
            DrawLine(batch, Font, HintFor(_page), new Vector2(40f, ContentTop), new Color(150, 165, 195));
            DrawButtons(batch);
        }

        private static string HintFor(Page page) => page switch
        {
            Page.KImage => "KImage 不设 Sprite 时用默认白纹 + Color 染色 → 纯色块；右块 Pivot=(0.5,0.5)，同 WorldPosition 下矩形以该点为中心展开。",
            Page.KButton => "九个 KButton 用 Anchor=Pivot=目标位置钉在固定矩形九处，文字 Alignment=MiddleCenter；悬停/按住可见四态过渡，点击计数。",
            Page.KLabel => "九个 KLabel 与 KButton 同构（Anchor 决定落点、Alignment 只管格内，统一 MiddleCenter）；下方开启 HorizontalWrap 演示折行。",
            Page.KSimpleLabel => "九个 KSimpleLabel 只用 Pivot 定位，文本按 Pivot 决定相对 WorldPosition 的锚点。",
            Page.KScrollView => "KScrollView：滚轮 / 右侧滚动条拖拽可滚动内容（视口外子项被剔除）；内容区可放任意 KWidget。",
            Page.KDropDown => "KDropDown：点击主体展开选项，选中后更新下方回显；点击展开区域之外自动收起。",
            Page.KInputBox => "KInputBox：点击聚焦后可键入（ASCII + 退格），回车提交；点击框外失焦。下方回显提交内容。",
            Page.KProgressBar => "KProgressBar：进度条在 0~1 间来回动画，支持四种方向与百分比文本（ShowText）。",
            _ => "",
        };

        public override void Dispose()
        {
            foreach (KButton b in _anchorButtons) b?.Unregister();
            _dd?.Unregister();
            _input?.Unregister();
            KInputMgr.Unregister(_scroll);

            foreach (KTransform root in new[] { _pageImage, _pageButton, _pageLabel, _pageSimple, _pageScroll, _pageDrop, _pageInput, _pageProgress })
                if (root != null) root.Parent = null;

            base.Dispose();
        }

        // ── 共用辅助 ─────────────────────────────────────────────────────────
        private static readonly string[] AnchorNames =
        [
            "LeftTop", "MiddleTop", "RightTop",
            "Left", "Middle", "Right",
            "LeftBottom", "MiddleBottom", "RightBottom",
        ];

        private static readonly string[] PivotNames =
        [
            "(0,0)", "(0.5,0)", "(1,0)",
            "(0,0.5)", "(0.5,0.5)", "(1,0.5)",
            "(0,1)", "(0.5,1)", "(1,1)",
        ];

        private static Color ButtonColor(int index) => new(
            60 + index % 3 * 40,
            90 + index / 3 * 40,
            170 - index % 3 * 25);

        private static Color Lighten(Color c, int d) => new(
            System.Math.Min(255, c.R + d), System.Math.Min(255, c.G + d), System.Math.Min(255, c.B + d));

        private static Color Darken(Color c, int d) => new(
            System.Math.Max(0, c.R - d), System.Math.Max(0, c.G - d), System.Math.Max(0, c.B - d));
    }
}
