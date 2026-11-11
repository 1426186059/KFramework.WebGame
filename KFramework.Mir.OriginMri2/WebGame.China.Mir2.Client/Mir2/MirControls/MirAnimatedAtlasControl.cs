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

        // 白鹭 MovieClip 每帧 (x,y) 偏移表：key = res 子图名。按当前帧名查表，对齐原始 MovieClip 表现。
        public Dictionary<string, Point> AtlasFrameOffsets { get; set; }

        protected override Point FrameDrawOffset
        {
            get
            {
                if (AtlasFrameOffsets != null &&
                    !string.IsNullOrEmpty(AtlasFrame)
                    && AtlasFrameOffsets.TryGetValue(AtlasFrame, out var off))
                {
                    return off;
                }
                return default;
            }
        }
    }
}
