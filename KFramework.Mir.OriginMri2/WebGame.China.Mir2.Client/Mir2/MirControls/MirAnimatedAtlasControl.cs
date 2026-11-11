using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    /// <summary>
    /// 图集帧动画控件：帧来源是图集帧名列表（而不是 MLibrary 索引或独立纹理）。
    /// 计时 / 循环 / 淡入 / AfterAnimation 全部由 MirAnimatedBase 提供，这里只负责「当前帧取哪张图」。
    /// </summary>
    public class MirAnimatedAtlasControl : MirAnimatedBase
    {
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
        /// scale = 界面缩放系数（如 SelectScene.ScaleF）。
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
            AtlasFrameScale = scale;
            AnimationDelay = movie.DelayMs;
            AtlasSheet = sheet;
            AtlasAnimationFrames = movie.PlayOrder;

            // Size 只用于布局/命中框；绘制尺寸来自 Layouts 的每帧矩形。
            var b = movie.Bounds;
            if (b.Width > 0 && b.Height > 0)
                Size = new Size(Math.Max(1, (int)(b.Width * scale)), Math.Max(1, (int)(b.Height * scale)));

            return true;
        }
    }
}
