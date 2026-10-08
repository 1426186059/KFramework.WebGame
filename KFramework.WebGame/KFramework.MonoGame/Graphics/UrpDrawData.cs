using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// SRP-Batcher 式的「逐物体常量」数据（96 字节，std140 布局）。
    /// <para>
    /// 照 Unity 的 <c>UnityPerDraw</c>：第一个字段就是 <c>unity_ObjectToWorld</c>，后面两条是
    /// 本引擎为 2D 精灵补的（UV 矩形、颜色）。
    /// </para>
    /// <para>
    /// <b>为什么是"常量"而不是顶点属性</b>：SRP Batcher 省的不是 DrawCall 次数，而是"换物体"这件事的开销 ——
    /// 它只剩下<b>改一次绑定</b>（<c>glBindBufferRange</c> 换个偏移），既不上传数据、也不设 uniform、也不切程序。
    /// 所以逐物体数据必须住在<b>一块常驻缓冲</b>里，而 uniform block 的布局受 std140 约束：
    /// 成员按 16 字节对齐（<c>mat4</c> 占 64、<c>vec4</c> 占 16），本结构体正好 96 字节、天然满足。
    /// 若再加一个 <c>float</c>，就得自己补齐到 16 的倍数，否则着色器读到的字段会整体错位。
    /// </para>
    /// <para>
    /// <b>硬件约束</b>：<c>bindBufferRange</c> 的偏移必须是 <c>UNIFORM_BUFFER_OFFSET_ALIGNMENT</c> 的整数倍
    /// （常见 256 字节），所以"每条记录实际占多少"由后端起缓冲时按该值向上取整，不直接用这里的 96。
    /// 见 <see cref="WebGL_ShaderProgram_2D_Urp"/>。
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UrpDrawData
    {
        /// <summary>对象→世界矩阵（照 Unity 的 <c>unity_ObjectToWorld</c>）：把单位四边形 (0,0)-(1,1) 变换到屏幕。</summary>
        public Matrix4x4 ObjectToWorld;

        /// <summary>逐物体 UV 矩形：xy = 起点、zw = 尺寸（归一化到 0~1）。</summary>
        public Vector4 UvRect;

        /// <summary>逐物体颜色（0~1）。</summary>
        public Vector4 Tint;

        /// <summary>这段数据本身占的字节数（不含 <c>UNIFORM_BUFFER_OFFSET_ALIGNMENT</c> 的对齐填充）。</summary>
        public const int SizeInBytes = 96;
    }
}
