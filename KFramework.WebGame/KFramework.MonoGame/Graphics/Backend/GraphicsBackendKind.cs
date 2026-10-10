namespace KFramework.MonoGame
{
    /// <summary>
    /// 渲染后端种类（实现见 <see cref="IGraphicsBackend"/> 的三个实现）。
    /// <para>
    /// 选择后端有两个入口：<see cref="GraphicsDevice(bool, GraphicsBackendKind)"/>（同步，WebGL2 / Canvas2D）
    /// 与 <see cref="GraphicsDevice.CreateAsync(GraphicsBackendKind, bool, bool)"/>（异步，WebGPU 必须走这条）。
    /// </para>
    /// </summary>
    public enum GraphicsBackendKind
    {
        /// <summary>WebGL 2.0：能力最全（着色器 / 实例化 / URP / 渲染目标），初始化同步。</summary>
        WebGL20 = 0,

        /// <summary>WebGPU：初始化异步（requestAdapter / requestDevice），不支持时可由工厂回落 WebGL 2.0。</summary>
        WebGPU = 1,

        /// <summary>
        /// Canvas2D：不走 GPU 管线，直接用浏览器 Canvas2D 回放精灵批次 —— 用于对照 / 兜底。
        /// 不支持自定义着色器、GPU 实例化、URP、渲染目标与压缩纹理（相关入口会抛 <see cref="System.NotSupportedException"/>）。
        /// </summary>
        Canvas2D = 2,
    }
}
