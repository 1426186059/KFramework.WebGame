namespace Mir
{
    /// <summary>
    /// 移植自 Unity 工程的 MirLog，去掉了对 Unity Debug / Client.Settings 的依赖，直接输出到控制台。
    /// </summary>
    public static class MirLog
    {
        public static bool Enabled { get; set; } = true;

        public static void Log(string v)
        {
            if (Enabled) Console.WriteLine(v);
        }

        public static void LogWarning(string v)
        {
            if (Enabled) Console.WriteLine("[WARN] " + v);
        }

        public static void LogError(string v)
        {
            if (Enabled) Console.WriteLine("[ERROR] " + v);
        }
    }
}
