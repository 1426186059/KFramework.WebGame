using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    /// <summary>
    /// 逐帧动画控件的公共基类：只负责「计时 / 帧偏移 / 循环 / 淡入 / 播完回调」，
    /// 不关心每一帧具体从哪来 —— 图集帧名由 MirAnimatedAtlasControl 提供，
    /// 已解码的纹理由 MirAnimatedTexture2DControl 提供。
    ///
    /// 所有实例统一登记在 Animations，由 CMain 每帧调用 UpdateOffSet() 驱动，
    /// 因此新增帧来源时不需要再改主循环。
    /// 计时结构与老的 MirAnimatedControl（MLibrary 索引版）保持一致。
    /// </summary>
    public abstract class MirAnimatedBase : MirImageControl
    {
        public static List<MirAnimatedBase> Animations = new List<MirAnimatedBase>();

        // 状态字段设为 protected：子类需要按 _animated / _offSet 决定当前显示哪一帧。
        protected bool _animated, _fadeIn, _loop;
        protected float _fadeInRate;
        protected long _fadeInDelay, _nextFadeTime, _nextOffSet, _animationDelay;
        protected int _animationCount, _offSet;

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
            if (AnimatedChanged != null) AnimatedChanged.Invoke(this, e);
        }

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
            if (AnimationCountChanged != null) AnimationCountChanged.Invoke(this, e);
        }

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
            if (AnimationDelayChanged != null) AnimationDelayChanged.Invoke(this, EventArgs.Empty);
        }

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
            if (FadeInChanged != null) FadeInChanged.Invoke(this, e);
        }

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
            if (FadeInRateChanged != null) FadeInRateChanged.Invoke(this, e);
        }

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
            if (FadeInDelayChanged != null) FadeInDelayChanged.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler AfterAnimation;

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
            if (LoopChanged != null) LoopChanged.Invoke(this, e);
        }

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
            if (OffSetChanged != null) OffSetChanged.Invoke(this, e);
        }

        protected MirAnimatedBase()
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
