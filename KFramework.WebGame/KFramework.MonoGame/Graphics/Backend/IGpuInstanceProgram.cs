namespace KFramework.MonoGame
{
    /// <summary>
    /// 后端侧的 GPU 实例化程序：由 <see cref="GpuInstanceBatch"/> 驱动。
    /// <para>
    /// 分工：材质状态、纹理绑定、渲染统计、分批、投影计算都在引擎侧（<see cref="GraphicsDevice"/> / <see cref="GpuInstanceBatch"/>），
    /// 后端只负责「程序 + 缓冲 + 一次实例化 draw」这部分平台相关的工作。
    /// </para>
    /// <para>
    /// 命名上与 <see cref="IShaderProgram"/> 同一层：都是"后端的着色器程序"，区别是这个程序带着
    /// 单位四边形缓冲与逐实例缓冲（<c>vertexAttribDivisor = 1</c>），一次 draw 覆盖 N 个实例。
    /// </para>
    /// </summary>
    internal interface IGpuInstanceProgram : IDisposable
    {
        /// <summary>单次 draw 的实例上限（实例数超过它时由上层分批）。</summary>
        int Capacity { get; }

        /// <summary>上传实例数据并发起一次实例化绘制（count ≤ <see cref="Capacity"/>，texture 已绑定）。</summary>
        void Draw(in Matrix4x4 transform, Span<GpuInstance> instances, int count, Texture2D texture);
    }
}
