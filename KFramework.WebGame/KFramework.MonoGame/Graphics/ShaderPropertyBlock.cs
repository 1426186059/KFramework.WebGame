using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 每一次绘制的着色器属性「覆盖块」：本引擎里对应 Unity 的 <c>MaterialPropertyBlock</c>。
    /// <para>
    /// <b>它解决的问题</b>：多个物体共用同一份绘制配置（同一个 <see cref="ShaderEffect"/>、同一套渲染状态），
    /// 但每个物体要能覆盖<b>不同的颜色 / 不同的 shader 变量</b>，而不必给每个物体复制一份效果或材质 ——
    /// 块挂在「这一次绘制」上，用完即弃。
    /// </para>
    /// <para>
    /// <b>与效果的关系</b>：<see cref="ShaderEffect"/> 上是"默认属性值"，块上是"这一次绘制的覆盖值" ——
    /// 绘制时先发效果的默认值、再发块里的值，同名以块为准（照 Unity 的 <c>SetPropertyBlock</c>）。
    /// </para>
    /// <para>
    /// <b>代价（CPU 合批路径，<see cref="SpriteBatch"/> 的逐顶点路径）</b>：块是<b>可变 uniform</b>，
    /// 值必须当场生效；一次 draw 只有一份 uniform 值，所以它<b>会切批</b> —— N 个各不相同的块就是 N 次 DrawCall。
    /// 换来的覆盖能力是：任意条数、任意属性名、任意类型（float / int / 向量 / 矩阵 / 纹理）。
    /// </para>
    /// <para>
    /// <b>GPU 实例化路径（<see cref="GpuInstanceBatch"/>）里不切批</b>：先用
    /// <see cref="Material.SetGpuInstanceChannels"/> 把属性声明成"实例通道"（照 Unity 的实例化属性；
    /// Unity 声明在 shader 的 <c>UNITY_INSTANCING_BUFFER</c> 里），再把这个块传给
    /// <see cref="GpuInstanceBatch.Draw(Texture2D, Rectangle?, Rectangle?, Color, float, Vector2, Vector2, SpriteEffects, float, ShaderPropertyBlock?)"/> ——
    /// 块里的值被编码进逐实例数据，<b>每个实例各自持有自己的属性值</b>，逐精灵不同也仍然只 1 次 DrawCall。
    /// 那条路没有 uniform 覆盖层，所以块里出现没被声明的属性时是直接抛异常（装不下的东西无处可去）。
    /// </para>
    /// <para>
    /// 与 Unity 的对应关系：Unity 里是 <c>renderer.SetPropertyBlock(block)</c>，本引擎里"渲染者"就是一次
    /// <see cref="SpriteBatch.Draw(Texture2D, Rectangle, Color, ShaderPropertyBlock?)"/>，块作为该次绘制的参数传入。
    /// 典型用法（共享效果 + 一个重复使用的块）：
    /// <code>
    /// batch.Begin(_sharedMaterial);
    /// foreach (var item in items)
    /// {
    ///     _block.Clear();
    ///     _block.SetColor("uTint", item.Color);
    ///     _block.SetFloat("uPulse", item.Pulse);
    ///     batch.Draw(tex, item.Rect, Color.White, _block);   // 块挂在这"一次绘制"上
    /// }
    /// batch.End();
    /// </code>
    /// </para>
    /// </summary>
    public sealed class ShaderPropertyBlock : ShaderProperties
    {
        /// <summary>清空全部属性（重复使用同一个块时先清再设）。</summary>
        public void Clear()
        {
            ClearProperties();
        }

        /// <summary>从另一个块整体拷贝（先清空再逐条覆盖）。</summary>
        public void CopyFrom(ShaderPropertyBlock source)
        {
            ArgumentNullException.ThrowIfNull(source);

            ClearProperties();
            foreach (KeyValuePair<string, ShaderProperty> pair in source.Properties)
                Set(pair.Key, pair.Value);
        }
    }
}
