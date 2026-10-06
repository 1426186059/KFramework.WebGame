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

        /// <summary>
        /// 是否为"逐帧动画"效果：为 true 时 GraphicsDevice 不对其做材质去重短路，
        /// 每帧都重新 Apply，以便把 uTime / 自定义参数等随时间变化的 uniform 灌入 GPU。
        /// 默认精灵着色器返回 false；自定义 <see cref="ShaderEffect"/> 返回 true。
        /// </summary>
        bool IsAnimated { get; }
    }

}
