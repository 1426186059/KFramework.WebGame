using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    // 图集动画控件：参照 MirAnimatedControl 的计时/循环结构，但帧来源是图集帧名列表（而非 MLibrary 索引）。
    public class MirAtlasAnimatedControl : MirImageControl
    {
        public static List<MirAtlasAnimatedControl> Animations = new List<MirAtlasAnimatedControl>();


        private bool _animated;
        public event EventHandler AnimatedChanged;
        public bool Animated
        {
            get { return _animated; }
            set
            {
                if (_animated == value) return;
                _animated = value;
                _nextOffSet = CMain.Time + _fadeInDelay;
                OnAnimatedChanged(EventArgs.Empty);
            }
        }
        protected virtual void OnAnimatedChanged(EventArgs e)
        {
            Redraw();
            if (AnimatedChanged != null)
                AnimatedChanged.Invoke(this, e);
        }


        private int _animationCount;
        public event EventHandler AnimationCountChanged;
        public virtual int AnimationCount
        {
            get { return _animationCount; }
            set
            {
                if (_animationCount == value) return;
                _animationCount = value;
                OnAnimationCountChanged(EventArgs.Empty);
            }
        }
        protected virtual void OnAnimationCountChanged(EventArgs e)
        {
            if (AnimationCountChanged != null)
                AnimationCountChanged.Invoke(this, e);
        }


        private long _animationDelay;
        public event EventHandler AnimationDelayChanged;
        public long AnimationDelay
        {
            get { return _animationDelay; }
            set
            {
                if (_animationDelay == value) return;
                _animationDelay = value;
                OnAnimationDelayChanged();
            }
        }
        protected virtual void OnAnimationDelayChanged()
        {
            if (AnimationDelayChanged != null)
                AnimationDelayChanged.Invoke(this, EventArgs.Empty);
        }


        private long _nextFadeTime;
        private bool _fadeIn;
        public event EventHandler FadeInChanged;
        public bool FadeIn
        {
            get { return _fadeIn; }
            set
            {
                if (_fadeIn == value) return;
                _nextFadeTime = CMain.Time + _fadeInDelay;
                _fadeIn = value;
                OnFadeInChanged(EventArgs.Empty);
            }
        }
        protected virtual void OnFadeInChanged(EventArgs e)
        {
            if (FadeInChanged != null)
                FadeInChanged.Invoke(this, e);
        }


        private float _fadeInRate;
        public event EventHandler FadeInRateChanged;
        public virtual float FadeInRate
        {
            get { return _fadeInRate; }
            set
            {
                if (_fadeInRate == value) return;
                _fadeInRate = value;
                OnFadeInRateChanged(EventArgs.Empty);
            }
        }
        protected virtual void OnFadeInRateChanged(EventArgs e)
        {
            if (FadeInRateChanged != null)
                FadeInRateChanged.Invoke(this, e);
        }


        private long _fadeInDelay;
        public event EventHandler FadeInDelayChanged;
        public long FadeInDelay
        {
            get { return _fadeInDelay; }
            set
            {
                if (_fadeInDelay == value) return;
                _fadeInDelay = value;
                OnFadeInDelayChanged();
            }
        }
        protected virtual void OnFadeInDelayChanged()
        {
            if (FadeInDelayChanged != null)
                FadeInDelayChanged.Invoke(this, EventArgs.Empty);
        }


        public event EventHandler AfterAnimation;


        // 逐帧帧名列表；设值时自动把 AnimationCount 设为帧数（与 OffSet 循环对齐）。
        private List<string> _atlasAnimationFrames;
        public List<string> AtlasAnimationFrames
        {
            get { return _atlasAnimationFrames; }
            set
            {
                _atlasAnimationFrames = value;
                if (_atlasAnimationFrames != null && _atlasAnimationFrames.Count > 0)
                    AnimationCount = _atlasAnimationFrames.Count;
            }
        }

        // 按“前缀 + 索引”批量生成逐帧帧名（prefix + i，i 从 beginIndex 到 endIndex，含两端）。
        public void SetFrames(string prefix, int beginIndex, int endIndex)
        {
            if (string.IsNullOrEmpty(prefix) || endIndex < beginIndex) return;
            var frames = new List<string>(endIndex - beginIndex + 1);
            for (int i = beginIndex; i <= endIndex; i++)
                frames.Add(prefix + i);
            AtlasAnimationFrames = frames;
        }

        // 直接给定逐帧帧名数组。
        public void SetFrames(params string[] frames)
        {
            AtlasAnimationFrames = (frames == null || frames.Length == 0) ? null : new List<string>(frames);
        }

        public override string AtlasFrame
        {
            get
            {
                if (_atlasAnimationFrames != null && _atlasAnimationFrames.Count > 0 && _animated)
                    return _atlasAnimationFrames[OffSet % _atlasAnimationFrames.Count];
                return base.AtlasFrame;
            }
            set { base.AtlasFrame = value; }
        }


        private bool _loop;
        public event EventHandler LoopChanged;
        public bool Loop
        {
            get { return _loop; }
            set
            {
                if (_loop == value) return;
                _loop = value;
                OnLoopChanged(EventArgs.Empty);
            }
        }
        protected virtual void OnLoopChanged(EventArgs e)
        {
            if (LoopChanged != null)
                LoopChanged.Invoke(this, e);
        }


        private int _offSet;
        public event EventHandler OffSetChanged;
        public virtual int OffSet
        {
            protected get { return _offSet; }
            set
            {
                if (_offSet == value) return;
                _offSet = value;
                OnOffSetChanged(EventArgs.Empty);
            }
        }
        protected virtual void OnOffSetChanged(EventArgs e)
        {
            OnIndexChanged();
            if (OffSetChanged != null)
                OffSetChanged.Invoke(this, e);
        }
        private long _nextOffSet;


        public MirAtlasAnimatedControl()
        {
            _loop = true;
            _nextFadeTime = CMain.Time;
            _nextOffSet = CMain.Time;
            Animations.Add(this);
        }

        public void UpdateOffSet()
        {
            if (_fadeIn && CMain.Time > _nextFadeTime)
            {
                if ((Opacity += _fadeInRate) > 1F)
                {
                    Opacity = 1F;
                    _fadeIn = false;
                }

                _nextFadeTime = CMain.Time + _fadeInDelay;
            }

            if (!Visible || !_animated || _animationDelay == 0 || _animationCount == 0) return;

            if (CMain.Time < _nextOffSet) return;

            Redraw();

            _nextOffSet = CMain.Time + _animationDelay;


            if (++OffSet < _animationCount) return;

            EventHandler temp = AfterAnimation;

            if (!Loop)
                Animated = false;
            else
                OffSet = 0;

            if (temp != null)
                temp.Invoke(this, EventArgs.Empty);
        }


        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            AnimatedChanged = null;
            _animated = false;

            AnimationCountChanged = null;
            _animationCount = 0;

            AnimationDelayChanged = null;
            _animationDelay = 0;

            AfterAnimation = null;

            LoopChanged = null;
            _loop = false;

            OffSetChanged = null;
            _offSet = 0;

            _nextOffSet = 0;

            Animations.Remove(this);
        }

    }
}
