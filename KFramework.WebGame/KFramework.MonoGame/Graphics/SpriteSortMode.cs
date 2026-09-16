namespace KFramework.MonoGame
{
    /// <summary>批处理排序策略。</summary>
    public enum SpriteSortMode
    {
        /// <summary>立即绘制，不合并批次（切换状态时调用 Draw 很方便，但性能最差）。</summary>
        Immediate,
        /// <summary>延迟到 End 时统一排序绘制（默认，批处理效果最好）。</summary>
        Deferred,
        /// <summary>按纹理排序，忽略深度。</summary>
        Texture,
        /// <summary>按深度从后往前（远的先画）。</summary>
        BackToFront,
        /// <summary>按深度从前往后。</summary>
        FrontToBack,
    }
}
