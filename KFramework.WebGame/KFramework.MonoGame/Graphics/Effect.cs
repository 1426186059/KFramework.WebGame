namespace KFramework.MonoGame
{
    /// <summary>
    /// 对应原版 MonoGame 的 <c>Effect</c>：着色器效果。
    /// <para>
    /// 本框架内部以 <see cref="IShaderProgram"/> 表示具体着色器程序（WebGL = GLSL Program，WebGPU = WGSL 模块 + 渲染管线），
    /// <see cref="Effect"/> 是其对外公开包装，用于 <see cref="SpriteBatch.Begin(SpriteSortMode, BlendState?, SamplerState?, DepthStencilState?, RasterizerState?, Effect?, Matrix4x4?)"/> 等
    /// 需要与原版 MonoGame 保持一致的 API。
    /// </para>
    /// <para>传 null（或不设置）时回落到 <see cref="GraphicsDevice.Effect"/> 的默认精灵着色器。</para>
    /// </summary>
    public class Effect : IDisposable
    {
        /// <summary>底层着色器程序，仅供 SpriteBatch / 后端内部使用。</summary>
        public IShaderProgram Program { get; }

        public Effect(IShaderProgram program)
        {
            Program = program ?? throw new ArgumentNullException(nameof(program));
        }

        public void Dispose() => Program.Dispose();
    }
}
