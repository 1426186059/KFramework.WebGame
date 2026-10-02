namespace KFramework.MonoGame
{
    /// <summary>
    /// 光栅化状态（本 2D 后端仅保留实际用到的成员）。
    /// GraphicsDevice 在属性 setter 里把 <see cref="CullMode"/> 翻译成 WebGL 的 CULL_FACE 开关。
    /// </summary>
    public sealed class RasterizerState
    {
        /// <summary>剔除模式，默认 <see cref="KFramework.MonoGame.CullMode.Back"/>（与 Unity 一致）。</summary>
        public CullMode CullMode { get; set; } = CullMode.Back;

        /// <summary>是否启用剪刀测试。</summary>
        public bool ScissorTestEnable { get; set; }

        /// <summary>创建默认（<see cref="CullMode.Back"/>）实例。</summary>
        public RasterizerState() { }

        private RasterizerState(CullMode cullMode, bool scissorTestEnable)
        {
            CullMode = cullMode;
            ScissorTestEnable = scissorTestEnable;
        }

        /// <summary>不剔除，双面都渲染（2D 精灵绘制默认用它，对应 Shader 的 <c>Cull Off</c>）。</summary>
        public static readonly RasterizerState CullNone = new(CullMode.Off, false);

        /// <summary>剔除面向摄像机的面（几何体"由内向外翻转"的效果，对应 <c>Cull Front</c>）。</summary>
        public static readonly RasterizerState CullFront = new(CullMode.Front, false);

        /// <summary>剔除背对摄像机的面（最常用的性能优化设置，对应 <c>Cull Back</c>）。</summary>
        public static readonly RasterizerState CullBack = new(CullMode.Back, false);
    }
}
