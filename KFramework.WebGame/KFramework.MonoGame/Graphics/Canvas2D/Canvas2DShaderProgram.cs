namespace KFramework.MonoGame
{
    /// <summary>
    /// Canvas2D 后端的"着色器程序"：Canvas2D 没有可编程管线，本类只把 <see cref="SpriteBatch"/> 交来的
    /// <b>view × 投影</b>矩阵还原成 Canvas2D 的 2×3 仿射（局部像素 → 画布像素），交给 TS 侧 setTransform。
    /// <para>
    /// 还原方式：投影固定是 <see cref="Matrix4x4.CreateOrthographicScreen"/>（像素 → NDC，Y 轴翻转）——
    /// 因为 <see cref="Canvas2DBackend.NeedsOffscreenYFlip"/> 恒为 false 且本后端不支持渲染目标。
    /// 于是 <c>view = projection × P⁻¹</c>，P⁻¹ 就是它的解析逆（NDC → 像素）。
    /// </para>
    /// <para>材质 uniform / 属性块在 Canvas2D 下没有意义，一律忽略（着色器路径由材质状态与顶点色承担）。</para>
    /// </summary>
    internal sealed class Canvas2DShaderProgram : IShaderProgram
    {
        private readonly Canvas2DBackend _backend;

        public Canvas2DShaderProgram(Canvas2DBackend backend) => _backend = backend;

        /// <summary>
        /// 恒为 true：Canvas2D 侧的变换是<b>模块级状态</b>（不是 GPU uniform），
        /// 画布改尺寸会把它连同裁剪一起清空，而 <see cref="GraphicsDevice.ApplyMaterial"/> 会按材质 / 变换去重、
        /// 状态没变就跳过下发 —— 那样就会出现"某帧之后所有内容都按单位变换画"的错误。
        /// 声明为"逐帧动画"可让上层每帧都重新 Apply，代价仅一次 JS 调用。
        /// </summary>
        public bool IsAnimated => true;

        public void Apply(Matrix4x4 projection, Material material, ShaderPropertyBlock? block)
        {
            int w = Math.Max(1, _backend.ViewportWidth);
            int h = Math.Max(1, _backend.ViewportHeight);

            // P⁻¹：NDC → 像素（x = (x_ndc + 1)·w/2，y = (1 − y_ndc)·h/2）
            Matrix4x4 pinv = Matrix4x4.Identity;
            pinv.M11 = w * 0.5f;
            pinv.M22 = -h * 0.5f;
            pinv.M41 = w * 0.5f;
            pinv.M42 = h * 0.5f;

            // 行向量约定：A * B = 先应用 A 再应用 B，故 projection × P⁻¹ 恰好消掉投影、剩下 view。
            Matrix4x4 view = projection * pinv;

            // Canvas2D 的矩阵是 x' = a·x + c·y + e、y' = b·x + d·y + f（列向量写法），
            // 与我们行向量约定的对应关系即下面这六个分量。
            JSBind_Canvas2D.SetProjection(view.M11, view.M12, view.M21, view.M22, view.M41, view.M42);
        }

        public void Dispose() { }
    }
}
