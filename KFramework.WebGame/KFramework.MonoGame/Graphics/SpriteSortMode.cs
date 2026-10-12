namespace KFramework.MonoGame
{
    /// <summary>
    /// 批处理排序策略。
    /// <para>
    /// <b>不含 MonoGame 的 <c>Immediate</c></b>：那是"每笔 Draw 当场提交、不做任何排序"，
    /// 在 Web 上等于回到"一个精灵一次 draw call"，与本引擎引入批处理的初衷相反。
    /// 本引擎里"逐笔立即生效"有正规通道 —— <see cref="ShaderPropertyBlock"/>：带块的绘制会当场提交
    /// （块是可变 uniform，值必须当场生效）；其余情况一律等 <c>End</c> 统一结算。
    /// </para>
    /// </summary>
    public enum SpriteSortMode
    {
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
