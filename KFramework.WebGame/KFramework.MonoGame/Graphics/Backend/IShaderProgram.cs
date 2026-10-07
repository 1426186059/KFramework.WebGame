namespace KFramework.MonoGame
{

    /// <summary>
    /// 着色器程序的后端抽象。
    /// <para>
    /// WebGL 后端 = GLSL Program（<see cref="WebGL_ShaderProgram_2D_Default"/>）；WebGPU 后端 = WGSL 模块 + 渲染管线。
    /// 上层（<see cref="SpriteBatch"/> / <see cref="SpriteBatcher"/>）只依赖本接口，不感知具体后端，
    /// 因此两种后端可以互换而不用改任何绘制代码。
    /// </para>
    /// </summary>
    /// <remarks>声明为 public 以便 <see cref="Material"/>（public）能暴露 <see cref="Material.Effect"/> 字段，且外部程序集（示例工程）可使用。</remarks>
    public interface IShaderProgram : IDisposable
    {
        /// <summary>
        /// 绑定本程序，写入投影矩阵与内置 uniform，并把三层着色器属性按「后者覆盖前者」灌入对应 uniform：
        /// <paramref name="effect"/>（效果自带值）→ <paramref name="material"/>（材质基线值）→
        /// <paramref name="properties"/>（本次绘制的覆盖块，MaterialPropertyBlock）。
        /// 属性名在着色器里不存在时忽略该属性（同 Unity 的行为）。
        /// </summary>
        void Apply(Matrix4x4 projection, Material material, MaterialPropertyBlock? properties, ShaderProperties effect);

        /// <summary>
        /// 是否为"逐帧动画"效果：为 true 时 GraphicsDevice 不对其做材质去重短路，
        /// 每帧都重新 Apply，以便把 uTime / 自定义参数等随时间变化的 uniform 灌入 GPU。
        /// 默认精灵着色器返回 false；自定义（<see cref="ShaderEffect"/> 带的）程序返回 true。
        /// </summary>
        bool IsAnimated { get; }
    }

}
