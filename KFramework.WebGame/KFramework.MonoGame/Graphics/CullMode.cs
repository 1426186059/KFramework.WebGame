namespace KFramework.MonoGame
{
    /// <summary>
    /// 剔除模式（照 <c>UnityEngine.Rendering.CullMode</c>，与 Shader 里的 <c>Cull</c> 命令一一对应）。
    /// <para>
    /// 三个成员按<b>面的朝向</b>划分（背对 / 面向摄像机），而不是按三角形的绕序 ——
    /// 用起来与 ShaderLab 里写 <c>Cull Back / Front / Off</c> 完全一致。
    /// </para>
    /// <para>
    /// 「正面」的判定依赖绕序约定：两个后端都在初始化时固定为 <b>CCW（逆时针）= 正面</b>
    /// （WebGL 的 <c>frontFace(CCW)</c>、WebGPU 管线描述里的 <c>"frontFace":"ccw"</c>）。
    /// 于是 <see cref="Back"/> 剔掉的实际上就是顺时针缠绕的面。
    /// </para>
    /// </summary>
    public enum CullMode
    {
        /// <summary>
        /// 不剔除，正反两面都渲染（对应 Shader 的 <c>Cull Off</c>）。
        /// 常用于透明物体、双面墙等特殊效果；2D 精灵绘制默认用它。
        /// </summary>
        Off = 0,

        /// <summary>
        /// 剔除<b>面向</b>摄像机的面（对应 Shader 的 <c>Cull Front</c>）。
        /// 效果相当于把几何体"由内向外翻转"——只留下原本看不见的内壁。
        /// </summary>
        Front = 1,

        /// <summary>
        /// 剔除<b>背对</b>摄像机的面（对应 Shader 的 <c>Cull Back</c>，Unity 的默认值）。
        /// 最常用的设置：不渲染看不见的面，省掉这部分光栅化与片元着色开销。
        /// </summary>
        Back = 2,
    }
}
