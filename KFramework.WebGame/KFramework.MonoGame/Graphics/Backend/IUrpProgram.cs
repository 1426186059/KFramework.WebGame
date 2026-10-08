using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 后端侧的 SRP-Batcher 式程序：由 <see cref="URPBatch"/> 驱动。
    /// <para>
    /// 与另两种后端程序的区别（三条路的数据通道完全不同）：
    /// <list type="bullet">
    ///   <item><description><see cref="IShaderProgram"/>（普通程序）：逐物体差异走<b>顶点数据</b>，uniform 逐个下发。</description></item>
    ///   <item><description><see cref="IGpuInstanceProgram"/>（GPU 实例化）：逐物体差异走<b>逐实例顶点属性</b>（divisor=1），一次 draw 画完整批。</description></item>
    ///   <item><description>本接口（SRP Batcher 式）：逐物体与逐材质数据都走 <b>uniform buffer</b>，
    ///   一次 <see cref="DrawSegment"/> 里先整段上传逐物体常量，然后逐个 <c>drawElements</c> ——
    ///   每次只重绑一次 UBO 范围，<b>不切程序、不设 uniform、不再上传</b>。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal interface IUrpProgram : IDisposable
    {
        /// <summary>单段能画的物体上限（超出由上层分段）。</summary>
        int Capacity { get; }

        /// <summary>
        /// 画一段物体：整段一次性上传逐物体常量，然后逐个 <c>drawElements</c>。
        /// </summary>
        /// <param name="projection">投影矩阵（一段只下发一次）。</param>
        /// <param name="draws">逐物体常量（长度 ≥ <paramref name="count"/>）。</param>
        /// <param name="count">本段物体数（≤ <see cref="Capacity"/>）。</param>
        /// <param name="texture">本段的纹理（已由上层绑定到 0 号单元）。</param>
        /// <param name="materialColor">材质常量（<c>UnityPerMaterial</c> 里的 uColorScale）。</param>
        /// <param name="uploadMaterial">是否需要上传材质常量（材质没变就传 false，一次都不传）。</param>
        /// <returns>实际发起的 draw 次数（= 物体数；SRP Batcher 不降 DrawCall）。</returns>
        int DrawSegment(in Matrix4x4 projection, Span<UrpDrawData> draws, int count,
                        Texture2D texture, in Vector4 materialColor, bool uploadMaterial);
    }
}
