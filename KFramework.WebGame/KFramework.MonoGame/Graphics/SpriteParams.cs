using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 逐精灵参数：8 个通道（P0~P7），每个取值 0~1，顶点里各占 1 字节（unorm8，精度 1/255）。
    /// <para>
    /// 定位（与 MaterialPropertyBlock 的分工）：
    /// <b>要覆盖任意属性/类型（甚至矩阵、纹理）就用 <see cref="MaterialPropertyBlock"/>，代价是"一次 draw 一份 uniform 值" → 块变即切批；
    /// 只要几个数值、而且必须合批，就用本结构（顶点属性，一顶点一份值）。</b>
    /// 本结构的值随顶点走 —— 既不参与分批键（分批键只有纹理与属性块），也不需要重新下发 uniform，
    /// 所以同一批里每个精灵都可以不一样。
    /// </para>
    /// <para>
    /// 语义：引擎只负责把这 8 个字节随顶点送到着色器里 <c>aParams0</c> / <c>aParams1</c> 两个 vec4 输入，
    /// <b>不做任何解释</b>。这 8 个通道叫什么、怎么参与计算，完全由着色器决定（乘子 / 混合权重 / UV 偏移 / 开关位都可以）。
    /// 例如"以材质为基线、按精灵覆盖"：
    /// <code>
    /// // 顶点着色器：in vec4 aParams0; out vec4 vParams0; ... vParams0 = aParams0;
    /// // 片元着色器：vParams0.a = 0 → 用材质基线，= 1 → 完全用逐精灵值
    /// c.rgb = mix(uBaseTint.rgb, vParams0.rgb, vParams0.a);
    /// </code>
    /// </para>
    /// <para>
    /// 边界：顶点通道只能携带数字，<b>装不下矩阵、纹理、shader 关键字、渲染状态</b>。
    /// 那些仍然只有 Material / MaterialPropertyBlock 能做（代价是每换一次外观切一次批）。
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SpriteParams
    {
        /// <summary>8 个通道各占 1 字节。</summary>
        public const int SizeInBytes = 8;

        /// <summary>8 个通道（0~1，硬件按 unorm 归一化，着色器里直接得到 0~1 的 float）。</summary>
        public readonly byte P0, P1, P2, P3, P4, P5, P6, P7;

        /// <summary>全 0（本类型的默认值）。常用于"混合权重"语义：0 = 不改动、完全用材质基线。</summary>
        public static readonly SpriteParams Zero = default;

        public SpriteParams(float p0, float p1, float p2, float p3, float p4, float p5, float p6, float p7)
        {
            P0 = ToByte(p0); P1 = ToByte(p1); P2 = ToByte(p2); P3 = ToByte(p3);
            P4 = ToByte(p4); P5 = ToByte(p5); P6 = ToByte(p6); P7 = ToByte(p7);
        }

        /// <summary>前 4 个通道取自 <paramref name="lo"/>，后 4 个取自 <paramref name="hi"/>（对应着色器的 aParams0 / aParams1）。</summary>
        public SpriteParams(Vector4 lo, Vector4 hi)
            : this(lo.X, lo.Y, lo.Z, lo.W, hi.X, hi.Y, hi.Z, hi.W) { }

        /// <summary>只填前 4 个通道（aParams0），后 4 个为 0。</summary>
        public SpriteParams(Vector4 lo) : this(lo, Vector4.Zero) { }

        /// <summary>前 4 个通道取自颜色（rgba → 0~1），后 4 个为 0。适合"逐精灵色调"这种最常见用法。</summary>
        public static SpriteParams FromColor(Color color)
            => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f, 0f, 0f, 0f, 0f);

        /// <summary>前 4 个通道取自颜色，后 4 个取自自定义值（例如相位 / 强度 / UV 偏移）。</summary>
        public static SpriteParams FromColor(Color color, Vector4 hi)
            => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f, hi.X, hi.Y, hi.Z, hi.W);

        private static byte ToByte(float value)
            => (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);
    }
}
