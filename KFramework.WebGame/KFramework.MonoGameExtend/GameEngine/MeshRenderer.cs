using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 自动合批渲染器的统一基类（对标 Unity 的 Renderer / MeshRenderer）。
    /// <para>
    /// 所有参与 <see cref="KCamera"/> 自动合批的渲染组件都继承它：相机扫描整棵场景树时只收集 MeshRenderer 节点，
    /// 分类顺序为 <see cref="RenderQueue"/>（第一优先级，不同队列强制断批）；同一队列内再按队列类型分两路——
    /// 不透明队列(Geometry/AlphaTest/Background) 按「世界 Z 深度 优先、<see cref="SortingOrder"/> 兜底」，
    /// 透明队列(Transparent/Overlay) 按「<see cref="SortingOrder"/> 优先、世界 Z 深度 兜底」（对标 Unity）。
    /// 最后顺序扫描：相邻且 <see cref="Material"/> 相同的节点合进同一个
    /// Begin/End 批，材质变化则自动补 End + Begin。即「每个物体自带材质（对标 Unity），相机据此分类、动态合批」。
    /// </para>
    /// <para>
    /// 本类只负责 Draw（往相机已开的批里画），不自己 Begin/End——Begin/End 由相机统一驱动。
    /// KDrawable（KCanvas 这类自己开批的容器）不是 MeshRenderer，会被相机跳过（不能嵌在已开的批里）。
    /// </para>
    /// </summary>
    public class MeshRenderer : KTransform
    {
        /// <summary>本渲染器期望的材质（对标 Unity 每个 Renderer 身上的 Material）。
        /// 为 null 时相机用默认材质。相机自动合批据此分类：相邻同材质 → 一个批；材质变 → 断批重开。</summary>
        public Material? Material { get; set; } = null;

        /// <summary>渲染排序顺序（对标 Unity Renderer.sortingOrder）。相机分类时先按此粗排序，同级再按世界 Z 深度：
        /// 即「SortingOrder 分组 → ZDepth 组内排序 → 相邻同材质合批」。</summary>
        public int SortingOrder { get; set; } = 0;

        /// <summary>渲染队列（对标 Unity renderQueue）。相机分类的<b>第一优先级</b>：不同队列之间强制断批，
        /// 桶内才按 SortingOrder → 世界 Z → 材质 细分。数值越大越后画（透明物体放 Transparent 之后）。</summary>
        public RenderQueue RenderQueue { get; set; } = RenderQueue.Geometry;

        // Draw 由具体子类（SpriteRenderer 等）重写；基类本身不绘制任何内容。
    }
}
