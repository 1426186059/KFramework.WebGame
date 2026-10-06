using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 每次绘制的材质属性「覆盖块」（照 Unity 的 <c>MaterialPropertyBlock</c>）。
    /// <para>
    /// <b>它要解决的问题只有一个</b>：多个物体<b>共用同一个材质</b>，但每个物体可以用<b>不同的颜色 / 不同的 shader 变量</b>，
    /// 而 <b>DrawCall 仍然能合并</b>。三个条件缺一不可 —— 如果做不到"合并 DrawCall"，
    /// 这个类就没有存在意义（那种需求直接改材质属性、或给每个物体一份材质实例就够了）。
    /// </para>
    /// <para>
    /// <b>本引擎怎么做到"合并"</b>：把块里被 <see cref="Material.SetSpriteChannels"/> 声明映射的属性值，
    /// 按通道编码进<b>顶点数据</b>（<see cref="SpriteParams"/> 的 8 个通道）。顶点数据不参与分批键，
    /// 于是同一批里每个物体的属性可以各不相同，而整批仍然只有一次 DrawCall ——
    /// 这与 Unity 靠实例化属性 / 常量缓冲把逐物体值搬离"材质状态"的思路是同一件事。
    /// </para>
    /// <para>
    /// <b>边界（必须知道）</b>：通道只有 8 个、且是 8 位归一化数值，<b>装不下矩阵与纹理</b>。
    /// 块里出现没被映射的属性时，这一次绘制会退回 uniform 覆盖路径：语义仍然正确，
    /// 但"一次 draw 只有一份 uniform 值"，所以它<b>会切批</b>（有几张这样的绘制就是几次 DrawCall）。
    /// 换句话说：能映射进通道的属性越多，合批越完整；映射不了的必须接受切批。
    /// </para>
    /// <para>
    /// 用法（一个共享材质 + 每物体一个块，整批 1 次 DrawCall）：
    /// <code>
    /// // 材质上声明：块的哪些属性按顺序进 8 个顶点通道（没声明的材质 = 块退回 uniform 路径）
    /// material.SetSpriteChannels("uTint.rgb", "uPulse", "uHue", "uFlip", "uMaskAmount", "uAngle");
    ///
    /// batch.Begin(material);
    /// foreach (var item in items)
    /// {
    ///     _block.Clear();
    ///     _block.SetColor("uTint", item.Tint);
    ///     _block.SetFloat("uPulse", item.Pulse);
    ///     batch.Draw(tex, item.Rect, Color.White, _block);   // 块挂在这"一次绘制"上
    /// }
    /// batch.End();   // → 全部合并成 1 次 DrawCall
    /// </code>
    /// </para>
    /// <para>
    /// 与 Unity 的对应关系：Unity 里是 <c>renderer.SetPropertyBlock(block)</c>，本引擎里"渲染者"就是一次
    /// <see cref="SpriteBatch.Draw(Texture2D, Rectangle, Color, MaterialPropertyBlock?)"/>，块作为该次绘制的参数传入。
    /// 典型用法（一个共享材质 + 一个重复使用的块，一次 Begin/End 画出 N 种外观）：
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
    /// <para>
    /// 代价要说清楚：块是<b>可变 uniform</b>，值必须当场生效，所以引擎会在带块的绘制上<b>立即提交</b>，
    /// 即「块一变就切一批」—— N 个各不相同的块就是 N 次 DrawCall。它换来的是覆盖能力：
    /// 任意条数、任意属性名、任意类型（float / int / 向量 / 矩阵 / 纹理）。
    /// 若需要"逐实例差异仍然合批"，请把参数放进顶点通道（<see cref="SpriteParams"/>，8 个数值通道）。
    /// </para>
    /// </summary>
    public sealed class MaterialPropertyBlock : MaterialProperties
    {
        /// <summary>清空全部属性（照 Unity 的 MaterialPropertyBlock.Clear，重复使用同一个 block 时先清再设）。</summary>
        public void Clear()
        {
            ClearProperties();
        }

        /// <summary>从另一个 block 整体拷贝（照 Unity 的 MaterialPropertyBlock.CopyFrom，先清空再逐条覆盖）。</summary>
        public void CopyFrom(MaterialPropertyBlock source)
        {
            ArgumentNullException.ThrowIfNull(source);

            ClearProperties();
            foreach (KeyValuePair<string, MaterialProperty> pair in source.Properties)
                Set(pair.Key, pair.Value);
        }
    }
}
