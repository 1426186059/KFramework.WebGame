using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 每次绘制的材质属性「覆盖块」（照 Unity 的 <c>MaterialPropertyBlock</c>）。
    /// <para>
    /// 用途与 Unity 完全对应：<b>不要为了改几个属性就去 new 一个材质</b>。
    /// 做法是共用一个 <see cref="Material"/>（着色器 + 渲染状态 + 属性基线），
    /// 再拿一个重复使用的 MaterialPropertyBlock，在每次绘制前把这一 draw 要改的少数属性盖上去：
    /// 绘制时先下发材质的属性、再下发 Block 的覆盖值（后设覆盖先设），所以一次 block 的改动只影响这一批绘制。
    /// </para>
    /// <para>
    /// 与 Unity 的对应关系：Unity 里是 <c>renderer.SetPropertyBlock(block)</c>，本引擎里渲染者是 SpriteBatch 的
    /// 一批绘制，故写成 <see cref="SpriteBatch.Begin(Material, MaterialPropertyBlock?, SpriteSortMode, Matrix4x4?)"/>。
    /// 典型用法（也是本引擎推荐的高效写法）：
    /// <code>
    /// _block.Clear();
    /// _block.SetColor("uTint", color);
    /// _block.SetFloat("uPulse", pulse);
    /// batch.Begin(_sharedMaterial, _block);   // 同一个材质 + 同一个 block，逐个实例改值
    /// batch.Draw(tex, rect, Color.White);
    /// batch.End();
    /// </code>
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
