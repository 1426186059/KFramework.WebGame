namespace KFramework.MonoGame
{

    /// <summary>
    /// 精灵着色器程序的后端抽象。
    /// <para>
    /// WebGL 后端 = GLSL Program（<see cref="SpriteEffect"/>）；WebGPU 后端 = WGSL 模块 + 渲染管线。
    /// 上层（<see cref="SpriteBatch"/> / <see cref="SpriteBatcher"/>）只依赖本接口，不感知具体后端，
    /// 因此两种后端可以互换而不用改任何绘制代码。
    /// </summary>
    /// <remarks>声明为 public 以便 <see cref="Material"/>（public）能暴露 Effect 字段，且外部程序集（示例工程）可使用。</remarks>
    public interface ISpriteProgram : IDisposable
    {
        /// <summary>绑定本程序并把投影矩阵写入 uniform。</summary>
        void Apply(Matrix4x4 projection);
    }

}
