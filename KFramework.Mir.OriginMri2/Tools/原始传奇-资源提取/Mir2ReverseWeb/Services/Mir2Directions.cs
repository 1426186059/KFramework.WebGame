namespace Mir2.Direction;

/// <summary>
/// 原始传奇 精灵方向系统（DirectionType + totaldir 方向缩减）。
/// 逆向自客户端 main10.25341.1.min.js 的 DirectionType 枚举与 Sprite/ActionLoader 的 getDir：
///   每个动作在配置（modelInfo / effect）里有 totaldir（1/2/5/8），getDir 据此把 8 个逻辑朝向
///   映射为实际存储朝向，引擎只加载这些“实际朝向”的帧，其余朝向靠镜像复用或根本不渲染。
/// 例：特效 totaldir=1 → 所有朝向折叠成 NONE(=DOWN=4)，整组只存/只取 1 帧（雷电术 12040014 即如此）；
///     totaldir=5 → 仅 0..4 为真帧，dir5~7 镜像自 3~1；totaldir=2 → 仅 {1,3}；totaldir=8 → 全 8 向。
///
/// 本类是该逻辑的权威实现：工具（Mir2ReverseWeb 的帧导出）与将来 WebGame.China.Mir2.Client 共用，
/// 保证“加载/下载哪些方向”两边行为完全一致，避免各写一套导致帧缺失或多余请求。
/// </summary>
public static class Mir2Directions
{
    // DirectionType 枚举（main10.25341.1.min.js @1594642）：8 个朝向 0..7
    public const int UP = 0, RIGHT_UP = 1, RIGHT = 2, RIGHT_DOWN = 3, DOWN = 4,
                     LEFT_DOWN = 5, LEFT = 6, LEFT_UP = 7;

    /// <summary>无朝向（totaldir=1）时统一折叠到的朝向；游戏里 NONE = DOWN = 4。</summary>
    public const int NONE = 4;

    // 镜像映射表（同 @1594642）
    /// <summary>totaldir=5（4 向真帧 + dir5~7 镜像）：dir5→3, dir6→2, dir7→1。</summary>
    private static readonly int[] EIGHT_MIRROR_DIRS = { 0, 1, 2, 3, 4, 3, 2, 1 };
    /// <summary>totaldir=2（2 向真帧：右=1 / 左下=3，其余镜像）：dir0~2→1, dir3~5→3, dir6~7→1。</summary>
    private static readonly int[] EIGHT_FROM_TWO_DIRS = { 1, 1, 1, 3, 3, 3, 1, 1 };

    /// <summary>
    /// 游戏 getDir(action,dir)：依据动作 totaldir，把逻辑朝向 <paramref name="requestedDir"/> 映射为实际存储朝向。
    /// totaldir=1→NONE(4)；totaldir=2→2 向镜像；totaldir=5→4 向镜像（仅 dir≥5 镜像）；其余（含 8 或未知）→原样 8 向。
    /// </summary>
    public static int GetDir(int totaldir, int requestedDir)
    {
        int d = ((requestedDir % 8) + 8) % 8;   // 归一化到 0..7
        return totaldir switch
        {
            1 => NONE,
            2 => EIGHT_FROM_TWO_DIRS[d],
            5 => d >= 5 ? EIGHT_MIRROR_DIRS[d] : d,
            _ => d,   // 8 向或未知：原样（与游戏 getActionCfg 缺配置时行为一致）
        };
    }

    /// <summary>
    /// 某 totaldir 下，实际需要加载/下载的“实际朝向”集合（去重）。
    /// totaldir=1→{4}；totaldir=2→{1,3}；totaldir=5→{0,1,2,3,4}；totaldir=8（或未知）→{0..7}。
    /// 工具据此只请求真实存在的帧，避免对镜像位/缺帧发无效请求。
    /// </summary>
    public static HashSet<int> ActualDirs(int totaldir)
    {
        var set = new HashSet<int>();
        for (int d = 0; d < 8; d++) set.Add(GetDir(totaldir, d));
        return set;
    }
}
