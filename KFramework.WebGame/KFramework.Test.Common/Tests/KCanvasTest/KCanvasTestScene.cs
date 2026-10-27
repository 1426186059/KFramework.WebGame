using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.KCanvasTest
{

    /// <summary>
    /// <see cref="KCanvas"/> 组件测试：<b>每个组件一个标签页</b>（照 <c>Example.Mario/Game/TestScreen.cs</c> 的用法）。
    /// <para>
    /// 与其它测试页的区别：本页测的是「节点树 + Anchor/Pivot 布局 + 输入分发」这套 UI 体系，
    /// 所以组件不是画在 <see cref="TestSceneBase"/> 的批里，而是挂到
    /// <see cref="KUIRoot"/> 的 <see cref="KCanvas"/> 上 ——
    /// <see cref="KSceneMgr"/> 每帧会自动 Update / Draw <c>DontDestroyOnLoadRoot</c> 的子树（且在场景之后），
    /// 因此组件天然显示在正文之上，本页只负责画背景 / 页签 / 说明。
    /// </para>
    /// <para>
    /// 页签与组件的点击互不干扰的做法：组件一律摆在页签区下方，且切换页签时把非当前页的
    /// <see cref="KButton"/> 一并 <c>Interactable = false</c>（<see cref="KButton"/> 注册在全局输入上，
    /// 光靠 <c>activeSelf</c> 不足以挡住命中）。
    /// </para>
    /// <para>
    /// <b>页 ① 与页 ② 是同一个套路的两个层次</b>：都挂在一个"固定矩形"下，用
    /// <c>Anchor = Pivot = 目标位置</c>（照 TestScreen）把控件钉在矩形的 4 角 / 4 边中点 / 中心；
    /// 区别只在"控件内部怎么摆内容"——KButton 是按钮文字居中，KLabel 是 <c>Alignment</c> 统一 MiddleCenter。
    /// 这样"控件落在哪（Anchor/Pivot）"与"内容在格内怎么摆（Alignment）"是分开可读的。
    /// </para>
    /// </summary>
    public sealed class KCanvasTestScene : TestSceneBase
    {
        public override string Title => "KCanvas 组件：KButton / KLabel / KImage / KSimpleLabel（按钮切页）";

        // ── 页签 ────────────────────────────────────────────────────────────
        private enum Page { KButton = 0, KLabel = 1, KImage = 2, KSimpleLabel = 3 }
        private Page _page = Page.KButton;

        /// <summary>用高层号，避免与引擎/示例自己的 Canvas 撞车。</summary>
        private const int CanvasLayer = 9;

        private KCanvas _canvas;

        // 每个页签一个子树根：切页就是切 activeSelf，组件本身不必反复重建。
        private KTransform _pageButton, _pageLabel, _pageImage, _pageSimple;

        /// <summary>九个"目标位置"（0 / 0.5 / 1 的组合）：既用于 Anchor 也用于 Pivot（照 TestScreen）。</summary>
        private static readonly Vector2[] NineAnchors =
        [
            new(0f, 0f), new(0.5f, 0f), new(1f, 0f),
            new(0f, 0.5f), new(0.5f, 0.5f), new(1f, 0.5f),
            new(0f, 1f), new(0.5f, 1f), new(1f, 1f),
        ];

        // ── 页 ①：KButton ──────────────────────────────────────────────────
        private KImage _buttonDemoRect;
        private readonly KButton[] _anchorButtons = new KButton[9];
        private int _clickCount;

        private static readonly string[] AnchorButtonNames =
        [
            "LeftTop", "MiddleTop", "RightTop",
            "Left", "Middle", "Right",
            "LeftBottom", "MiddleBottom", "RightBottom",
        ];

        // ── 页 ②：KLabel（同样挂在固定矩形下，Alignment 只负责格内）────────
        private KImage _labelDemoRect;
        private readonly KLabel[] _anchorLabels = new KLabel[9];
        private KLabel _wrapLabel;

        private static readonly string[] AnchorNames =
        [
            "UpperLeft", "UpperCenter", "UpperRight",
            "MiddleLeft", "MiddleCenter", "MiddleRight",
            "LowerLeft", "LowerCenter", "LowerRight",
        ];

        // ── 页 ③：KImage（尺寸 / 染色 / Pivot）─────────────────────────────
        private KImage _imgBig, _imgTint, _imgMidPivot;

        // ── 页 ④：KSimpleLabel（Pivot 定位在九处）──────────────────────────
        private readonly KSimpleLabel[] _pivotLabels = new KSimpleLabel[9];
        private Rectangle _pivotDemoRect;

        private static readonly string[] PivotNames =
        [
            "(0,0)", "(0.5,0)", "(1,0)",
            "(0,0.5)", "(0.5,0.5)", "(1,0.5)",
            "(0,1)", "(0.5,1)", "(1,1)",
        ];

        private IFont _font;

        private const string WrapSample =
            "KLabel.HorizontalWrap = Wrap：超过控件宽度就在这里折行（内部走 TextRenderer 的折行）。";

        public override void LoadContent()
        {
            _font = KDefaultRes.DefaultSpriteFont;

            // 组件树挂在 Canvas 上（Canvas 自己挂在 KSceneMgr.DontDestroyOnLoadRoot，由 KSceneMgr 驱动）。
            _canvas = KUIRoot.Instance.GetCanvas(CanvasLayer);

            _pageButton = AddRoot(active: true);
            _pageLabel = AddRoot(active: false);
            _pageImage = AddRoot(active: false);
            _pageSimple = AddRoot(active: false);

            BuildButtonPage();
            BuildLabelPage();
            BuildImagePage();
            BuildSimpleLabelPage();
        }

        private KTransform AddRoot(bool active)
        {
            var root = new KTransform
            {
                Parent = _canvas,
                activeSelf = active,
            };
            return root;
        }

        // ── 页 ①：KButton ──────────────────────────────────────────────────

        private void BuildButtonPage()
        {
            // "固定矩形"：九个按钮都挂它下面，用 Anchor / Pivot 钉在它的 4 角 + 4 边中点 + 中心。
            // 与 Example.Mario/TestScreen 写法一致 —— 那里也是 Anchor = Pivot = 目标位置、AnchorPosition = 0
            // （关键：父节点是 KWidget 时，Anchor 以【父的尺寸】为基准；TestScreen 也是挂在自己的 KUIBase 下）。
            _buttonDemoRect = new KImage
            {
                Parent = _pageButton,
                Color = new Color(26, 34, 52),
            };

            for (int i = 0; i < 9; i++)
            {
                Vector2 anchor = NineAnchors[i];
                Color background = ButtonColor(i);
                int index = i;

                var button = new KButton
                {
                    Parent = _buttonDemoRect,
                    Size = new Vector2(160, 44),
                    Anchor = anchor,
                    Pivot = anchor,
                    AnchorPosition = Vector2.Zero,
                    Color = background,
                    NormalColor = background,
                    HighlightedColor = Lighten(background, 40),
                    PressedColor = Darken(background, 40),
                    Label = new KLabel(AnchorButtonNames[i], Color.White, _font)
                    {
                        // 按钮文字一律居中：显式写死 MiddleCenter（KLabel 默认是按 Pivot 推导，写出来更醒目）。
                        Alignment = TextAnchor.MiddleCenter,
                    },
                };

                button.PointerClickEvent += (_, _) =>
                {
                    _clickCount++;
                    button.Label.Text = $"{AnchorButtonNames[index]} ×{_clickCount}";
                };

                _anchorButtons[i] = button;
            }
        }

        // ── 页 ②：KLabel ───────────────────────────────────────────────────

        private void BuildLabelPage()
        {
            // 同样的"固定矩形"套路：控件落点交给 Anchor/Pivot，格内对齐交给 Alignment。
            _labelDemoRect = new KImage
            {
                Parent = _pageLabel,
                Color = new Color(26, 34, 52),
            };

            for (int i = 0; i < 9; i++)
            {
                Vector2 anchor = NineAnchors[i];

                _anchorLabels[i] = new KLabel(AnchorNames[i], ButtonColor(i), _font)
                {
                    Parent = _labelDemoRect,
                    // 控件自身尺寸：Anchor 只决定这个矩形"落在哪"，不改变它的大小。
                    Size = new Vector2(180, 40),
                    Anchor = anchor,
                    Pivot = anchor,
                    AnchorPosition = Vector2.Zero,
                    // 格内文字统一居中 —— 与页 ① 的按钮文字呼应；
                    // 想单独看"格内对齐"的话把这里改成别的 TextAnchor 即可，控件位置不受影响。
                    Alignment = TextAnchor.MiddleCenter,
                };
            }

            // 折行示例：挂在页根（非 KWidget 父 → 用视口尺寸当 Anchor 基准，这里直接绝对定位）。
            _wrapLabel = new KLabel(WrapSample, new Color(200, 230, 255), _font)
            {
                Parent = _pageLabel,
                Alignment = TextAnchor.UpperLeft,
                HorizontalWrap = HorizontalWrapMode.Wrap,
            };
        }

        // ── 页 ③：KImage ──────────────────────────────────────────────────

        private void BuildImagePage()
        {
            // 不设 Sprite 时 KImage 会用默认 1×1 白纹，配合 Color 就是"纯色块"。
            _imgBig = new KImage { Parent = _pageImage, Color = new Color(70, 130, 200) };
            _imgTint = new KImage { Parent = _pageImage, Color = new Color(230, 150, 90) };
            _imgMidPivot = new KImage { Parent = _pageImage, Color = new Color(140, 200, 140), Pivot = Vector2.One * 0.5f };
        }

        // ── 页 ④：KSimpleLabel（Pivot 定位）────────────────────────────────

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

        private static Color ButtonColor(int index) => new(
            60 + index % 3 * 40,
            90 + index / 3 * 40,
            170 - index % 3 * 25);

        private static Color Lighten(Color c, int delta) => new(
            Math.Min(255, c.R + delta), Math.Min(255, c.G + delta), Math.Min(255, c.B + delta));

        private static Color Darken(Color c, int delta) => new(
            Math.Max(0, c.R - delta), Math.Max(0, c.G - delta), Math.Max(0, c.B - delta));

        // ── 布局 ───────────────────────────────────────────────────────────

        private void Layout()
        {
            BeginButtons(68);
            AddUiButton("① KButton（固定矩形九锚点）", () => SetPage(Page.KButton), _page == Page.KButton);
            AddUiButton("② KLabel（同样九锚点 · 格内居中）", () => SetPage(Page.KLabel), _page == Page.KLabel);
            AddUiButton("③ KImage（尺寸 · 染色 · Pivot）", () => SetPage(Page.KImage), _page == Page.KImage);
            AddUiButton("④ KSimpleLabel（Pivot 定位）", () => SetPage(Page.KSimpleLabel), _page == Page.KSimpleLabel);

            // 组件区：页签下方，左右留边。
            float top = ContentTop + 34f;
            float left = 40f;
            float rectW = Device.Viewport.Width - left * 2f;

            switch (_page)
            {
                case Page.KButton: LayoutButtons(left, top); break;
                case Page.KLabel: LayoutLabels(left, top, rectW); break;
                case Page.KImage: LayoutImages(left, top); break;
                default: LayoutSimpleLabels(left, top, rectW); break;
            }
        }

        private static void Place(KWidget widget, float x, float y, float w, float h)
        {
            widget.Size = new Vector2(w, h);
            widget.WorldPosition = new Vector2(x, y);
        }

        private void LayoutButtons(float x, float y)
        {
            // 只需摆好这个"固定矩形"：九个按钮的 Anchor 都相对它，
            // 父尺寸一变会触发 OnParentWidgetSizeChanged 自动重排，无需逐个摆按钮。
            float rectW = Math.Min(Device.Viewport.Width - x * 2f, 960f);
            Place(_buttonDemoRect, x, y, rectW, 340f);
        }

        private void LayoutLabels(float x, float y, float width)
        {
            // 同样只摆容器；九个 KLabel 的落点由 Anchor 决定，会随容器尺寸自动重排。
            float rectW = Math.Min(Device.Viewport.Width - x * 2f, 960f);
            Place(_labelDemoRect, x, y, rectW, 300f);

            // 折行示例在容器下方（它是绝对定位，单独摆）。
            Place(_wrapLabel, x, y + 300f + 20f, width, 86f);
        }

        private void LayoutImages(float x, float y)
        {
            Place(_imgBig, x, y, 240f, 120f);
            Place(_imgTint, x + 270f, y, 120f, 120f);
            // Pivot 在中心：同样给 WorldPosition，控件矩形会以该点为中心展开。
            Place(_imgMidPivot, x + 440f + 60f, y + 60f, 120f, 120f);
        }

        private void LayoutSimpleLabels(float x, float y, float width)
        {
            float demoH = 220f;
            _pivotDemoRect = new Rectangle((int)x, (int)y, (int)width, (int)demoH);

            // 九个 Pivot 值分别落在演示矩形的 4 角 + 4 边中点 + 中心。
            for (int i = 0; i < 9; i++)
            {
                var point = new Vector2(
                    x + NineAnchors[i].X * width,
                    y + NineAnchors[i].Y * demoH);
                _pivotLabels[i].WorldPosition = point;
            }
        }

        private void SetPage(Page page)
        {
            _page = page;

            _pageButton.activeSelf = page == Page.KButton;
            _pageLabel.activeSelf = page == Page.KLabel;
            _pageImage.activeSelf = page == Page.KImage;
            _pageSimple.activeSelf = page == Page.KSimpleLabel;

            // KButton 注册在全局输入系统上，仅靠 activeSelf 挡不住命中 —— 一并关掉交互。
            bool buttonsLive = page == Page.KButton;
            foreach (KButton button in _anchorButtons)
                button.Interactable = buttonsLive;
        }

        public override void Update()
        {
            base.Update();
            Layout();

            // 中心那个按钮兼作状态回显：把当前 SelectionState 写进文字（四态颜色过渡的直接对照）。
            if (_page == Page.KButton)
                _anchorButtons[4].Label.Text = $"Middle（{_anchorButtons[4].SelectionState}）";

            ClickButtons();
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            // 组件由 KCanvas 自己 Begin/End 批并绘制（KSceneMgr 在场景之后驱动），
            // 所以这里只画"底板与说明"，别把组件的区域盖住。
            Layout();

            string hint;
            switch (_page)
            {
                case Page.KButton:
                    hint = "九个 KButton 照 Example.Mario/TestScreen 的写法：Anchor = Pivot = 目标位置、AnchorPosition = 0，"
                         + "钉在那个深色\"固定矩形\"的 4 角 / 4 边中点 / 中心；文字一律 Alignment = MiddleCenter。"
                         + "悬停 / 按住可见四态颜色过渡，点中心那个会把 SelectionState 回显到文字上。";
                    break;
                case Page.KLabel:
                    hint = "与页 ① 完全同构：九个 KLabel 同样用 Anchor = Pivot 钉在固定矩形的九处，"
                         + "Alignment 统一 MiddleCenter —— 两层分开读：Anchor 决定控件落在哪，Alignment 决定文字在格内怎么摆。"
                         + "下方那个开启 HorizontalWrap = Wrap 演示折行。";
                    break;
                case Page.KImage:
                    hint = "KImage 不设 Sprite 时用默认 1×1 白纹 + Color 染色 → 纯色块；"
                         + "右边那块把 Pivot 设为 (0.5,0.5)，同样的 WorldPosition 下矩形以该点为中心展开。";
                    break;
                default:
                    hint = "九个 KSimpleLabel 只用 Pivot 定位：WorldPosition 落在演示矩形的角 / 边 / 中心，"
                         + "文本按 Pivot 决定相对该点的锚点 —— 与 KLabel 的 Alignment 是两套不同的对齐机制。";
                    break;
            }

            DrawLine(batch, Font, hint, new Vector2(40f, ContentTop), new Color(150, 165, 195));

            if (_page == Page.KSimpleLabel)
            {
                // 演示矩形：一眼看出九个 Pivot 对应的锚点位置。
                DrawBorder(batch, _pivotDemoRect, new Color(70, 90, 130));
                foreach (Vector2 pivot in NineAnchors)
                {
                    int cx = (int)(_pivotDemoRect.X + pivot.X * _pivotDemoRect.Width);
                    int cy = (int)(_pivotDemoRect.Y + pivot.Y * _pivotDemoRect.Height);
                    DrawRect(batch, new Rectangle(cx - 3, cy, 7, 1), new Color(120, 150, 200));
                    DrawRect(batch, new Rectangle(cx, cy - 3, 1, 7), new Color(120, 150, 200));
                }
            }

            DrawButtons(batch);
        }

        private void DrawBorder(SpriteBatch batch, Rectangle rect, Color color)
        {
            DrawRect(batch, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            DrawRect(batch, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            DrawRect(batch, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            DrawRect(batch, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }

        public override void Dispose()
        {
            // KButton 注册在全局输入系统上，离开场景前必须注销，否则会泄漏（它会在别的页面继续吃点击）。
            foreach (KButton button in _anchorButtons)
                button?.Unregister();

            // 组件树挂在 KUIRoot 缓存的 Canvas 上（会被复用），摘掉自己的根节点。
            foreach (KTransform root in new[] { _pageButton, _pageLabel, _pageImage, _pageSimple })
            {
                if (root != null) root.Parent = null;
            }

            base.Dispose();
        }
    }

}
