namespace KFramework.Example3;

/// <summary>
/// 本地化字符串桩。源工程通过 FCGame_MonoGame.Content.Localization 的 resx 生成 Resources 类，
/// 这里只补齐游戏实际用到的几个键，避免引入整套 resx 内容管线。
/// </summary>
internal static class Resources
{
    public static string 分数 => "分数";
    public static string 时间 => "时间";
    public static string 开始游戏 => "开始游戏";
    public static string 设置 => "设置";
    public static string 新游戏 => "新游戏";
}
