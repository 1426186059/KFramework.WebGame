// 统一日志输出：所有模块共用，自动加 [时:分:秒] 时间戳。
// 用法：PrintTool.Write("DB", "消息内容");
internal static class PrintTool
{
    public static void Write(string tag, string message)
    {
        System.Console.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] [{tag}] {message}");
    }

    public static void Log(string message)
    {
        System.Console.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] [] {message}");
    }
}
