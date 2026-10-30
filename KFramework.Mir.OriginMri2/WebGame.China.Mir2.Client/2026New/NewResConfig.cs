using KFramework.MonoGame;

/// <summary>
/// 地图资源状态记录。
/// 原「地图图片 Lib 远程直链（按地图名从 Mir2Res/Map/&lt;地图名&gt;/ 拉取）」方案依赖本地 :5081
/// 资源服务器，已随该服务器移除；地图 Lib 现统一走默认 lib 通道（RemoteWebSetting.LibBaseUrl
/// 或 CDN），不按地图区分物理文件，故不再需要远程拉取与换图重载逻辑。
/// 此处仅保留当前地图名状态（由 MapCode 进图时写入），供诊断日志使用。
/// </summary>
public static class NewResConfig
{
    public static string CurrentMapName { get; internal set; } = "";
}
