namespace KFramework.MonoGame
{
    /// <summary>
    /// 后端侧的实例化绘制器：由 <see cref="SpriteBatchGPUInstance"/> 驱动。
    /// <para>
    /// 分工：材质状态、纹理绑定、渲染统计、分批、投影计算都在引擎侧（<see cref="GraphicsDevice"/> / <see cref="SpriteBatchGPUInstance"/>），
    /// 后端只负责「程序 + 缓冲 + 一次实例化 draw」这部分平台相关的工作。
    /// </para>
    /// </summary>
    internal interface ISpriteInstancer : IDisposable
    {
        /// <summary>单次 draw 的实例上限（实例数超过它时由上层分批）。</summary>
        int Capacity { get; }

        /// <summary>上传实例数据并发起一次实例化绘制（count ≤ <see cref="Capacity"/>，texture 已绑定）。</summary>
        void Draw(in Matrix4x4 transform, Span<SpriteInstance> instances, int count, Texture2D texture);
    }
}
