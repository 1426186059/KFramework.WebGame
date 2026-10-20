namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 渲染队列（对标 Unity 的 renderQueue / RenderPipeline 队列桶）。作为相机自动合批分类的<b>第一优先级</b>：
    /// 相机扫描全树后先按 RenderQueue 分桶（不同队列之间强制断批），桶内再按 SortingOrder → 世界 Z → 材质 细分。
    /// 数值越大越后画（透明物体通常放 Transparent 之后）。
    /// </summary>
    public enum RenderQueue
    {
        /// <summary>背景层（最先画，如天空盒 / 远裁剪背景）。</summary>
        Background = 0,

        /// <summary>不透明几何体（默认）。</summary>
        Geometry = 1000,

        /// <summary>Alpha 测试 / 镂空（在 Opaque 之后、透明之前）。</summary>
        AlphaTest = 2000,

        /// <summary>透明物体（半透明混合，依赖前面已画的不透明内容）。</summary>
        Transparent = 3000,

        /// <summary>最上层覆盖（UI 覆盖、后期合成等，最后画）。</summary>
        Overlay = 4000,
    }
}
