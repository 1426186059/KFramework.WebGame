using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 「批处理嵌套」：<b>用 <see cref="Begin"/> / <see cref="End"/> 配对来嵌套</b> ——
    /// 已经开批时再调一次 <see cref="Begin"/> 就叠一层（换材质），对应的 <see cref="End"/>
    /// 冲刷完这一层后自动切回外层材质；最外层的 <see cref="End"/> 才真正结束整批。
    /// <para>
    /// <b>为什么是「通过 Material」</b>：本引擎里一个批的状态恰好就是一个材质（混合 / 采样 / 深度 / 剔除
    /// + 效果程序），所以"嵌套的边界"就是"材质的切换点"：叠一层 = 冲刷当前层 → 换材质开一层；
    /// 退一层 = 冲刷当前层 → 重新下发外层材质。内部只持有一个 <see cref="SpriteBatch"/>
    /// （不按层 new 实例，顶点池只有一份）+ 一个材质栈。
    /// </para>
    /// <para>
    /// <b>三条要记住的约束</b>：
    /// <list type="bullet">
    ///   <item><description><b>跨边界不排序</b>：每层内部按 <see cref="SpriteSortMode"/> 排序，层与层之间按调用顺序
    ///   （不同材质 = 不同批次，Unity 同理）。</description></item>
    ///   <item><description><b>叠一层 / 退一层各至少 1 次 DrawCall</b>：冲刷一次就是一次提交 —— 这是"分层材质"的必然代价。</description></item>
    ///   <item><description><b>严格后进先出</b>：<see cref="Begin"/> 与 <see cref="End"/> 必须配对（嵌套时每个 Begin 都有一个 End
    ///   在等着），早失败好过静默画错。</description></item>
    /// </list>
    /// 嵌套与 <see cref="ShaderPropertyBlock"/> 是正交的：块在同一层里仍会当场切批，两者可以叠加。
    /// </para>
    /// <para>
    /// <b>本类只走"材质"这条路</b>：<see cref="Begin"/> 收 <see cref="Material"/>，不传（null）时用本类自带的默认材质
    /// （精灵默认状态 + 设备默认效果）—— 也就是<b>不使用</b> <see cref="SpriteBatch"/> 那套 MonoGame 风格的
    /// <c>Begin(SpriteSortMode, BlendState?, …)</c>。
    /// </para>
    /// <para>用法：</para>
    /// <code>
    /// var nested = new SpriteNestedBatch(Device);
    ///
    /// nested.Begin(materialUi);                        // 外层材质
    /// nested.Draw(panel, panelRect, Color.White);      // 用外层材质画
    ///
    /// nested.Begin(materialGlow);                      // 叠一层：冲刷外层 → 换发光材质
    /// nested.Draw(glow, glowRect, Color.White);        // 用发光材质画
    ///
    /// nested.Begin(materialMask);                      // 想嵌多深就再 Begin
    /// nested.Draw(mask, maskRect, Color.White);
    /// nested.End();                                    // 退到发光层（外层材质被重新下发）
    ///
    /// nested.End();                                    // 退到外层
    /// nested.Draw(panel2, panel2Rect, Color.White);    // 继续用外层材质
    ///
    /// int drawCalls = nested.End();                    // 最外层 End：整批结束，返回最后一段的 DrawCall
    /// </code>
    /// </summary>
    public sealed class SpriteNestedBatch
    {
        private readonly GraphicsDevice _device;

        /// <summary>真正干活的批（只有一份顶点池）；每一层就是"同一个批 + 不同材质"。</summary>
        private readonly SpriteBatch _batch;

        /// <summary>材质栈：栈底 = 外层材质，栈顶 = 当前层（栈里放的都是"生效中"的材质，不会是 null）。</summary>
        private readonly Stack<Material> _materials = new();

        /// <summary>
        /// 调用方不传材质时用的默认材质（精灵默认状态 + 设备默认效果）。
        /// 本类只走"材质"这条路：<b>不</b>调用 <see cref="SpriteBatch"/> 那套 MonoGame 风格老 <c>Begin</c>。
        /// </summary>
        private readonly Material _defaultMaterial = new();

        private SpriteSortMode _sortMode;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private bool _begun;

        /// <summary>创建嵌套批处理器（与 <see cref="SpriteBatch"/> 一样只给设备，材质在 <see cref="Begin"/> 里给）。</summary>
        public SpriteNestedBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;
            _batch = new SpriteBatch(device);
        }

        /// <summary>底层设备（对应 <see cref="SpriteBatch.GraphicsDevice"/>）。</summary>
        public GraphicsDevice GraphicsDevice => _device;

        /// <summary>当前嵌套深度（1 = 只有外层；未 Begin 时为 0）。</summary>
        public int Depth => _materials.Count;

        /// <summary>当前层的材质（未 Begin 时为 null；调用方没传材质时就是本类的默认材质）。</summary>
        public Material? CurrentMaterial => _materials.TryPeek(out Material? material) ? material : null;

        /// <summary>本批的排序模式（嵌套只换材质，不换排序与坐标系）。</summary>
        public SpriteSortMode SortMode => _sortMode;

        /// <summary>
        /// 开批 / 叠一层：<b>第一次调用</b>开批（材质 / 渲染顺序 / 相机矩阵在这里给）；
        /// <b>已经开批时再调用就是嵌套</b> —— 先把当前层已排队的绘制冲刷掉（顺序不能乱），再换材质开一层。
        /// <para>
        /// 嵌套时排序模式与相机矩阵沿用外层（嵌套只换材质，层与层之间不重新排序），
        /// 所以 <paramref name="sortMode"/> / <paramref name="transformMatrix"/> 只在第一次调用时生效。
        /// </para>
        /// </summary>
        /// <param name="material">这一层的材质；不传（null）= 用本类的默认材质。</param>
        /// <param name="sortMode">渲染顺序，见 <see cref="SpriteSortMode"/>（仅首次 Begin 生效）。</param>
        /// <param name="transformMatrix">相机矩阵，null = 单位矩阵（仅首次 Begin 生效）。</param>
        public void Begin(Material? material = null,
                          SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          Matrix4x4? transformMatrix = null)
        {
            Material level = material ?? _defaultMaterial;

            if (!_begun)
            {
                _sortMode = sortMode;
                _transform = transformMatrix ?? Matrix4x4.Identity;
                _materials.Clear();
                _begun = true;
            }
            else
            {
                EndLevel();     // 嵌套的边界 = 材质切换点：先把当前层冲刷掉
            }

            _materials.Push(level);
            BeginLevel(level);
        }

        /// <summary>
        /// 关掉一层：先把当前层已排队的绘制冲刷掉；如果还有外层，就<b>重新下发外层材质</b>并继续这批
        /// （上一层已经覆盖过设备状态，这一步不能省，只是设备侧去重会让它变便宜）；
        /// 已经在最外层则结束整批（之后 <see cref="Begin"/> 就是新的一批）。
        /// </summary>
        /// <returns>本次冲刷产生的 DrawCall 数（这一段的段数：正常是 1，空段是 0）。</returns>
        public int End()
        {
            if (!_begun) throw new InvalidOperationException("End 必须在 Begin 之后调用。");

            int draws = EndLevel();
            _materials.Pop();

            if (_materials.Count > 0)
            {
                BeginLevel(_materials.Peek());   // 回到外层材质（会被重新下发）
            }
            else
            {
                _begun = false;                  // 最外层：整批结束
            }

            return draws;
        }

        /// <summary>
        /// 开一层：只走"材质"这条路（调用方没给材质时传的是 <see cref="_defaultMaterial"/>），
        /// 因此不碰 <see cref="SpriteBatch"/> 的 MonoGame 风格老 <c>Begin</c>。
        /// </summary>
        private void BeginLevel(Material material) => _batch.Begin(material, _sortMode, _transform);

        /// <summary>冲刷当前层（交给 <see cref="SpriteBatch.End"/>），返回它产生的 DrawCall 数。</summary>
        private int EndLevel()
        {
            long before = _device.Metrics.DrawCount;
            _batch.End();
            return (int)(_device.Metrics.DrawCount - before);
        }

        // ============ 绘制透传（签名与 SpriteBatch 逐一对应，换用不必改调用代码）============

        public void Draw(Texture2D texture, Vector2 position, Color color)
            => _batch.Draw(texture, position, color);

        public void Draw(Texture2D texture, Vector2 position, Color color, float rotation, Vector2 origin, float scale, float layerDepth = 0f)
            => _batch.Draw(texture, position, color, rotation, origin, scale, layerDepth);

        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
            => _batch.Draw(texture, position, sourceRectangle, color);

        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth = 0f,
                         ShaderPropertyBlock? properties = null)
            => _batch.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth, properties);

        public void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
            => _batch.Draw(texture, destinationRectangle, sourceRectangle, color, rotation, origin, effects, layerDepth);

        public void Draw(Texture2D texture, Rectangle destination, Color color)
            => _batch.Draw(texture, destination, color);

        public void Draw(Texture2D texture, Rectangle destination, Rectangle? sourceRectangle, Color color)
            => _batch.Draw(texture, destination, sourceRectangle, color);

        public void Draw(Texture2D texture, Vector2 position, Color color, ShaderPropertyBlock? properties)
            => _batch.Draw(texture, position, color, properties);

        public void Draw(Texture2D texture, Rectangle destination, Color color, ShaderPropertyBlock? properties)
            => _batch.Draw(texture, destination, color, properties);

        public void Draw(Texture2D texture, Rectangle destination, Rectangle? sourceRectangle, Color color, ShaderPropertyBlock? properties)
            => _batch.Draw(texture, destination, sourceRectangle, color, properties);

        public void DrawCentered(Texture2D texture, Vector2 center, Color color,
                                 float rotation = 0f, float scale = 1f, float layerDepth = 0f)
            => _batch.DrawCentered(texture, center, color, rotation, scale, layerDepth);

        public void DrawString(IFont font, string text, Vector2 position, Color color)
            => _batch.DrawString(font, text, position, color);

        public void DrawString(IFont font, string text, Vector2 position, Color color,
                               float rotation, Vector2 origin, float scale, float layerDepth = 0f)
            => _batch.DrawString(font, text, position, color, rotation, origin, scale, layerDepth);
    }
}
