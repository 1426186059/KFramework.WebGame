using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 进度条：背景（继承 KImage） + 前景填充条（内部 KImage），进度 0~1。
    /// 支持四种填充方向；可选在中间显示百分比文本。挂在 KCanvas 下即自动 Update/Draw。
    /// <para>裁剪说明同 KScrollView：本框架 SpriteBatch 锁定 rasterizer，运行时 scissor 不生效，
    /// 这里前景条用"缩小 Size + 锚定端点"的方式精确绘制，不依赖 scissor，因此无溢出问题。</para>
    /// </summary>
    public class KProgressBar : KImage
    {
        public enum KProgressDir { LeftToRight, RightToLeft, BottomToTop, TopToBottom }

        private KImage _fill;
        private KLabel _textLabel;
        private float _progress;
        private bool _dirty = true;

        public float Progress
        {
            get => _progress;
            set
            {
                float v = value < 0f ? 0f : (value > 1f ? 1f : value);
                if (_progress != v) { _progress = v; _dirty = true; OnProgressChanged?.Invoke(this, _progress); }
            }
        }

        public Color FillColor { get; set; } = new Color(80, 200, 120);
        public Color BackgroundColor { get; set; } = new Color(40, 40, 40);
        public KProgressDir Direction { get; set; } = KProgressDir.LeftToRight;
        public bool ShowText { get; set; } = false;
        public Color TextColor { get; set; } = Color.White;
        public event EventHandler<float> OnProgressChanged;

        public KProgressBar()
        {
            Color = BackgroundColor;

            _fill = new KImage();
            _fill.Parent = this;
            _fill.Color = FillColor;

            _textLabel = new KLabel();
            _textLabel.Parent = this;
            _textLabel.MinMaxAnchor = RectangleF.MinMax(0, 0, 1, 1);
            _textLabel.Anchor = new Vector2(0.5f, 0.5f);
            _textLabel.Pivot = new Vector2(0.5f, 0.5f);
            _textLabel.AnchorOffset = AnchorRectOffset.Zero;
            _textLabel.Color = TextColor;
            _textLabel.activeSelf = false;
        }

        public override void Update()
        {
            if (_dirty) { LayoutFill(); _dirty = false; }
            if (_textLabel.activeSelf)
                _textLabel.Text = ((int)(_progress * 100)).ToString() + "%";
        }

        private void LayoutFill()
        {
            float w = Size.X * _progress;
            float h = Size.Y * _progress;
            switch (Direction)
            {
                case KProgressDir.LeftToRight:
                    _fill.Pivot = new Vector2(0f, 0.5f);
                    _fill.LocalPosition = new Vector2(0f, Size.Y * 0.5f);
                    _fill.Size = new Vector2(w, Size.Y);
                    break;
                case KProgressDir.RightToLeft:
                    _fill.Pivot = new Vector2(1f, 0.5f);
                    _fill.LocalPosition = new Vector2(Size.X, Size.Y * 0.5f);
                    _fill.Size = new Vector2(w, Size.Y);
                    break;
                case KProgressDir.BottomToTop:
                    _fill.Pivot = new Vector2(0.5f, 1f);
                    _fill.LocalPosition = new Vector2(Size.X * 0.5f, Size.Y);
                    _fill.Size = new Vector2(Size.X, h);
                    break;
                case KProgressDir.TopToBottom:
                    _fill.Pivot = new Vector2(0.5f, 0f);
                    _fill.LocalPosition = new Vector2(Size.X * 0.5f, 0f);
                    _fill.Size = new Vector2(Size.X, h);
                    break;
            }
        }

        public void SetTextVisible(bool visible) => _textLabel.activeSelf = visible;
    }
}
