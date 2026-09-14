using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2;

/// <summary>
/// 玩法用的精灵节点：等价于 PixiJS 的 Sprite。
/// <para>
/// 关键：重写 <see cref="KWidget.OnParentChanged"/> 为空实现，
/// 避免 KWidget 的锚点布局系统（UpdateRealRectangle）在 addChild（即 Parent = 容器）时
/// 把 <see cref="KTransform.LocalPosition"/> 覆盖回 (0,0)。
/// 这样它的 LocalPosition / LocalScale / Pivot 行为与 Pixi 的 position / scale / anchor 完全一致：
///   Parent   → addChild
///   LocalPosition → position
///   LocalScale    → scale
///   Pivot    → anchor
/// </para>
/// </summary>
internal sealed class TGameSprite : KImage
{
    protected override void OnParentChanged()
    {
        // 自由变换：不调用基类 UpdateRealRectangle，保持手动设置的 LocalPosition 生效。
    }
}
