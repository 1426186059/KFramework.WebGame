using Server;
using Server.MirEnvir;
using System.Threading;
using System.Text;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;

namespace Mir2Server
{
    /// <summary>
    /// Mir2 服务器控制台入口。
    /// 移植自 Crystal 的 Server.Library + Shared，并参考 Mir3Server 的做法：
    /// 用 WebSocket 作为传输层（浏览器 WASM 客户端可直接连接），上层仍是原版 Mir2 字节帧协议。
    /// 游戏数据需放在运行目录下的 Envir/ 与 Configs/（与 Crystal 原版一致）。
    /// </summary>
    class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetConsoleCP(uint wCodePageID);

        static void Main(string[] args)
        {
            // 让中文日志在控制台正常显示（兼容 GBK 代码页的旧控制台）
            try { SetConsoleOutputCP(65001); SetConsoleCP(65001); } catch { }
            Console.OutputEncoding = Encoding.UTF8;

            // 原版日志全部经 MessageQueue -> log4net 输出，但控制台没有配置 appender，
            // 因此原先日志被丢弃。这里挂一个 ConsoleAppender，覆盖 Server/Debug/Chat 全部日志。
            var layout = new PatternLayout("%date{yyyy-MM-dd HH:mm:ss} %-5level %logger - %message%newline");
            layout.ActivateOptions();

            var consoleAppender = new ConsoleAppender
            {
                Layout = layout,
                Threshold = Level.Info,
                Name = "ConsoleAppender",
            };
            consoleAppender.ActivateOptions();

            BasicConfigurator.Configure(consoleAppender);

            Settings.Load();

            // 打印 WebSocket 监听地址与端口，方便客户端直接复制连接
            Console.WriteLine("WebSocket 服务器地址: ws://{0}:{1}", Settings.IPAddress, Settings.Port);

            // 服务端必须以“服务器”视角解析收到的客户端数据包：
            // Library.Packet.ReceivePacket 依据静态 IsServer 在 GetClientPacket / GetServerPacket 间选择，
            // 未置 true 时会把客户端包误判为服务器包（如 ClientVersion 被当成 S.Connected），导致 InvalidCastException。
            Packet.IsServer = true;

            Envir.Main.Start();

            Console.WriteLine("Mir2 服务器已启动（WebSocket）。按 Ctrl+C 停止。");

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = false;
                Envir.Main.Stop();
            };

            while (Envir.Main.Running)
                Thread.Sleep(200);
        }
    }
}
