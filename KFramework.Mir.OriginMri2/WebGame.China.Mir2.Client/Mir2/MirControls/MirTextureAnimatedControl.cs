using Texture2D = KFramework.MonoGame.Texture2D;

namespace Client.MirControls
{
    /// <summary>
    /// 纹理序列动画控件。
    /// 参照 MirAtlasAnimatedControl 的计时 / 循环 / AfterAnimation 结构，但帧来源是已经解码好的
    /// Texture2D[]（例如登录过场帧的预载结果），而不是图集帧名或 MLibrary 索引。
    /// 继承 MirAtlasAnimatedControl 以复用 CMain 每帧的 UpdateOffSet() 驱动，无需改主循环。
    ///
    /// 用法：Frames = 帧数组；AnimationDelay = 每帧毫秒；Loop = false；Animated = true；
    ///       AfterAnimation += 播放结束回调。绘制走 MirImageControl 已有的 Texture 通道。
    /// </summary>
    public class MirTextureAnimatedControl : MirAtlasAnimatedControl
    {
        private Texture2D[] _frames;

        /// <summary>逐帧纹理；设值时把 AnimationCount 对齐为帧数，并立即显示第 0 帧。</summary>
        public Texture2D[] Frames
        {
            get { return _frames; }
            set
            {
                _frames = value;
                AnimationCount = value == null ? 0 : value.Length;
                ApplyFrame();
            }
        }

        public MirTextureAnimatedControl()
        {
            Loop = false;
            // 尺寸由使用者按帧原始尺寸给定，不随 Texture 变化。
            AutoSize = false;
        }

        protected override void OnOffSetChanged(EventArgs e)
        {
            ApplyFrame();
            base.OnOffSetChanged(e);
        }

        private void ApplyFrame()
        {
            if (_frames == null || _frames.Length == 0) return;
            int i = OffSet;
            if (i < 0) i = 0;
            // 基类在播放结束那一拍会把 OffSet 推到 AnimationCount，钳到最后一帧避免闪回第 0 帧。
            if (i >= _frames.Length) i = _frames.Length - 1;
            Texture = _frames[i];
        }
    }
}
