namespace KFramework.MonoGame
{
    /// <summary>
    /// 光栅化状态（照 MonoGame 的 RasterizerState，本 2D 后端仅保留实际用到的成员）。
    /// GraphicsDevice 在属性 setter 里把 <see cref="CullMode"/> 翻译成 WebGL 的 CULL_FACE 开关。
    /// </summary>
    public sealed class RasterizerState
    {
        /// <summary>背面剔除模式（照 MonoGame）。</summary>
        public CullMode CullMode { get; set; } = CullMode.CullCounterClockwiseFace;

        /// <summary>是否启用剪刀测试（照 MonoGame）。</summary>
        public bool ScissorTestEnable { get; set; }

        /// <summary>创建默认（CullCounterClockwise）实例。</summary>
        public RasterizerState() { }

        private RasterizerState(CullMode cullMode, bool scissorTestEnable)
        {
            CullMode = cullMode;
            ScissorTestEnable = scissorTestEnable;
        }

        /// <summary>不剔除（照 MonoGame 的 RasterizerState.CullNone）。</summary>
        public static readonly RasterizerState CullNone = new(CullMode.None, false);

        /// <summary>剔除顺时针背面（照 MonoGame 的 RasterizerState.CullClockwise）。</summary>
        public static readonly RasterizerState CullClockwise = new(CullMode.CullClockwiseFace, false);

        /// <summary>剔除逆时针背面（照 MonoGame 的 RasterizerState.CullCounterClockwise，SpriteBatch 默认）。</summary>
        public static readonly RasterizerState CullCounterClockwise = new(CullMode.CullCounterClockwiseFace, false);
    }
}
