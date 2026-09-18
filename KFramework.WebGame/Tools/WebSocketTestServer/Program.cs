using System.Net;

namespace WebSocketTestServer;

/// <summary>
/// WebSocket 测试服务器入口：给 KFramework.Example1 的「WebSocket 测试（客户端）」页当服务端。
/// <para>用法：<c>dotnet run --project Tools/WebSocketTestServer -- [端口]</c>，默认 9000，
/// 客户端连接地址 ws://127.0.0.1:9000/。</para>
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        int port = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 9000;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var server = new WsEchoServer(IPAddress.Any, port);
        await server.RunAsync(cts.Token).ConfigureAwait(false);
    }
}
