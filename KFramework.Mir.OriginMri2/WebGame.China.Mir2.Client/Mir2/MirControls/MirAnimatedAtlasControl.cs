using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    /// <summary>
    /// 图集帧动画控件：帧来源是图集帧名列表（而不是 MLibrary 索引或独立纹理）。
    /// 计时 / 循环 / 淡入 / AfterAnimation 全部由 MirAnimatedBase 提供，这里只负责「当前帧取哪张图」。
    /// </summary>
    public class MirAnimatedAtlasControl : MirAnimatedBase
    {
        /// <summary>
        /// 设 true 时，AtlasNameKey 自动加载完成后若图集是白鹭 MovieClip（mc/res 时间线），
        /// 自动调用 SetMovieClip 装配帧序/帧率/布局，外部无需再手动 SetFrames/AnimationDelay。
        /// 普通图集（无 MovieClip）不受影响，仍按外部 SetFrames 的帧名列表播放。
        /// </summary>
        public bool AutoSetupMovieClip { get; set; } = true;

        public MirAnimatedAtlasControl()
        {
            // 图集异步加载完成后（基类已赋值 AtlasSheet），若是 MovieClip 则自动装配。
            AtlasLoaded += (s, e) =>
            {
                if (!AutoSetupMovieClip) return;
                if (AtlasSheet != null && AtlasSheet.MovieClip != null)
                {
                    SetMovieClip(AtlasSheet);
                    Redraw();
                }
            };
        }

        // 逐帧帧名列表；设值时自动把 AnimationCount 设为帧数（与 OffSet 循环对齐）。
        private List<string> _atlasAnimationFrames;
        public List<string> AtlasAnimationFrames
        {
            get { return _atlasAnimationFrames; }
            set
            {
                _atlasAnimationFrames = value;
                if (_atlasAnimationFrames != null && _atlasAnimationFrames.Count > 0)
                {
                    AnimationCount = _atlasAnimationFrames.Count;
                }
            }
        }

        // 按「前缀 + 索引」批量生成逐帧帧名（prefix + i，i 从 beginIndex 到 endIndex，含两端）。
        public void SetFrames(string prefix, int beginIndex, int endIndex)
        {
            if (string.IsNullOrEmpty(prefix) || endIndex < beginIndex)
            {
                return;
            }

            var frames = new List<string>(endIndex - beginIndex + 1);
            for (int i = beginIndex; i <= endIndex; i++)
            {
                frames.Add(prefix + i);
            }
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

        // 当前装配的 MovieClip 时序对象（持有图集，负责帧序/帧率/统一注册点布局）。
        private EgretMovieClip _movieClip;

        protected override bool TryGetMovieClipFrameRect(out KFramework.MonoGame.Rectangle rect)
        {
            rect = default;
            if (_movieClip == null || string.IsNullOrEmpty(AtlasFrame)) return false;
            return _movieClip.Layouts.TryGetValue(AtlasFrame, out rect);
        }

        /// <summary>
        /// 装配一个白鹭 MovieClip：帧序 / 帧率 / 统一注册点布局 / 缩放 / 布局包围盒
        /// 全部由 Mir2AtlasSheet 组合的 EgretMovieClip 提供，控件只负责按当前帧取矩形来画。
        /// scale = 界面等比缩放系数（如 SelectScene.ScaleF），非 1 时写入 SizeScale（X/Y 同值）；
        /// 使用方可自行给 SizeScale 设不同分量做非等比，默认不覆盖。
        /// 返回 false 表示图集为空或没有可播放帧；同一 MovieClip 重复调用不重装配，不打断播放。
        /// </summary>
        public bool SetMovieClip(Mir2AtlasSheet sheet, float scale = 1f)
        {
            EgretMovieClip movie = sheet?.MovieClip;
            if (movie == null || movie.PlayOrder == null || movie.PlayOrder.Count == 0) return false;
            if (_movieClip == movie && AtlasAnimationFrames != null && AtlasAnimationFrames.Count > 0)
                return true;

            _movieClip = movie;
            AutoSize = false;
            // scale 非 1 时（显式指定界面缩放）覆盖 SizeScale；默认 1 时不覆盖使用方预置值。
            if (scale != 1f)
                SizeScale = new KFramework.MonoGame.Vector2(scale, scale);
            AnimationDelay = movie.DelayMs;
            AtlasSheet = sheet;
            AtlasAnimationFrames = movie.PlayOrder;

            // Size 只用于布局/命中框；Size getter 已含 SizeScale.X/Y，这里用原始包围盒，避免重复乘。
            var b = movie.Bounds;
            if (b.Width > 0 && b.Height > 0)
                Size = new Size(Math.Max(1, b.Width), Math.Max(1, b.Height));

            return true;
        }
    }
}
