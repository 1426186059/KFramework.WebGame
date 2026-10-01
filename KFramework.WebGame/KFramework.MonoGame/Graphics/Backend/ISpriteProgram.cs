namespace KFramework.MonoGame
{

    /// <summary>
    /// 精灵着色器程序的后端抽象。
    /// <para>
    /// WebGL 后端 = GLSL Program（<see cref="SpriteEffect"/>）；WebGPU 后端 = WGSL 模块 + 渲染管线。
    /// 上层（<see cref="SpriteBatch"/> / <see cref="SpriteBatcher"/>）只依赖本接口，不感知具体后端，
    /// 因此两种后端可以互换而不用改任何绘制代码。
    /// </para>
    /// </summary>
    internal interface ISpriteProgram : IDisposable
    {
        /// <summary>绑定本程序并把投影矩阵写入 uniform。</summary>
        void Apply(Matrix4x4 projection);
    }

}
