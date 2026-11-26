/// <summary>
/// 资源加载优先级（上层语义）。数值越大越优先，与引擎层 ContentManager 的 int priority 数值一致；
/// 传给引擎时由各上层入口强转为 int，引擎层本身不引用本枚举。
/// </summary>
public enum EResLoadPriority
{
    /// <summary>后台静默预载（懒队列）：最低，仅在无更急请求时空闲暖缓存。</summary>
    Background = 0,

    /// <summary>当前在屏、正在绘制的他人/怪物/NPC：次高。</summary>
    Visible = 5,

    /// <summary>自己（本体+装备）与同屏可见物品图标：高于他人，低于 UI。</summary>
    User = 8,

    /// <summary>UI 资源（登录界面、加载进度条、MirImageControl 远程图）：先于玩家模型，确保首屏立即可见。</summary>
    UI = 9,

    /// <summary>地图瓦片/地图数据：最高，进图/传送后第一时间出地面。</summary>
    Map = 10,
}
