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
        // 由 SetMovieClip 统一装配，外部不要自行赋值。
        public Dictionary<string, Point> AtlasFrameOffsets { get; private set; }

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

        /// <summary>
        /// 用一张白鹭 MovieClip 图集一次性装配动画：
        /// 帧序 / 帧率 / 逐帧注册点偏移 / 原生帧尺寸绘制 / 统一缩放 / 布局包围盒全在这里算好，
        /// 调用方不要再逐字段设置 AtlasSheet、AtlasAnimationFrames、AtlasFrameOffsets 等。
        /// scale = 界面缩放系数（如 SelectScene.ScaleF）。
        /// 返回 false 表示图集为空或没有可播放帧；同一张表重复调用不重装配，不打断播放。
        /// </summary>
        public bool SetMovieClip(Mir2AtlasSheet sheet, float scale = 1f)
        {
            if (sheet == null) return false;
            // 已装配过同一张表：SetInfo 重入时只由调用方刷新播放态，这里不重置动画。
            if (AtlasSheet == sheet && AtlasAnimationFrames != null && AtlasAnimationFrames.Count > 0)
                return true;

            long delay = 250;
            List<string> order = null;
            Dictionary<string, Point> offsets = null;
            var movie = sheet.MovieFactory?.First;
            if (movie != null)
            {
                delay = movie.DelayMs;
                order = movie.PlayOrder;                 // 已按 duration 展开，逐帧定格时长还原

                // mc.frames 的 x/y 是每帧子图左上角相对【MovieClip 注册点】的绝对坐标
                // （carrer1_1 实测 y+h 恒为 348，即脚底对齐）。直接拿来当绘制位移会把整组帧
                // 推离槽位；以首帧为基准归一化，只保留帧间 1~3px 的补偿量。
                int baseX = movie.Frames.Count > 0 ? movie.Frames[0].X : 0;
                int baseY = movie.Frames.Count > 0 ? movie.Frames[0].Y : 0;
                offsets = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in movie.Frames)
                    offsets[f.ResId] = new Point(f.X - baseX, f.Y - baseY);
            }

            if (order == null)
                order = sheet.PlayOrder != null && sheet.PlayOrder.Count > 0
                    ? new List<string>(sheet.PlayOrder)
                    : (sheet.Frames != null ? new List<string>(sheet.Frames.Keys) : null);
            if (order == null || order.Count == 0) return false;

            // 每帧按自身原始尺寸绘制、全帧共用同一缩放，脚底自然对齐，不会忽左忽右。
            AutoSize = false;
            AtlasFrameNativeSize = true;
            AtlasFrameScale = scale;
            AnimationDelay = delay;
            AtlasFrameOffsets = offsets;
            AtlasSheet = sheet;
            AtlasAnimationFrames = order;

            // Size 只用于布局/命中框，取所有帧的最大包围盒；绘制尺寸由各帧自己决定。
            int maxW = 0, maxH = 0;
            if (sheet.Frames != null)
                foreach (string frameName in order)
                    if (sheet.Frames.TryGetValue(frameName, out var fr))
                    {
                        if (fr.SourceRectangle.Width > maxW) maxW = fr.SourceRectangle.Width;
                        if (fr.SourceRectangle.Height > maxH) maxH = fr.SourceRectangle.Height;
                    }
            if (maxW > 0 && maxH > 0)
                Size = new Size(Math.Max(1, (int)(maxW * scale)), Math.Max(1, (int)(maxH * scale)));

            return true;
        }
    }
}
