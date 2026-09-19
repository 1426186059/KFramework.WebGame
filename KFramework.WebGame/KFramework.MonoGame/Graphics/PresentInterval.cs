namespace KFramework.MonoGame
{
    /// <summary>
    /// 定义 <see cref="GraphicsDevice"/> 呈现时如何更新窗口（照 MonoGame 的 PresentInterval）。
    /// </summary>
    public enum PresentInterval
    {
        /// <summary>等同 <see cref="One"/>。</summary>
        Default,
        /// <summary>等待垂直回扫后再更新，呈现频率不超过屏幕刷新率。</summary>
        One,
        /// <summary>每隔两次垂直回扫才更新一次（约半刷新率）。</summary>
        Two,
        /// <summary>立即更新窗口，帧率不设上限。</summary>
        Immediate,
    }
}
