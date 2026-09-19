using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace WebSocketTestServer;

/// <summary>
/// 给 KFramework 例子1 的 WebSocket 测试页当服务端的极简 WebSocket 服务器（RFC 6455，明文 ws://）。
///
/// <para>只依赖 BCL：TcpListener 收连接 → 手写 HTTP Upgrade 握手 → 按帧收发。
/// 行为：文本帧 echo 回发送者并广播给其他客户端；二进制帧回长度；ping 回 pong。</para>
///
/// <para>注意：浏览器页面若通过 https 打开，无法连接明文 ws（mixed content 会被拦），
/// 本地开发请用 http 页面（kfc / dotnet run 起的本地站点）。</para>
/// </summary>
internal sealed class WsEchoServer
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int MaxFrameBytes = 4 * 1024 * 1024;

    private readonly TcpListener _listener;
    private readonly int _port;

    private readonly object _gate = new();
    private readonly Dictionary<int, NetworkStream> _clients = new();

    private int _nextId;

    public WsEchoServer(IPAddress address, int port)
    {
        _port = port;
        _listener = new TcpListener(address, port);
    }

    public async Task RunAsync(CancellationToken token)
    {
        _listener.Start();
        Console.WriteLine($"[服务器] WebSocket 测试服务器已启动：ws://127.0.0.1:{_port}/");
        Console.WriteLine("[服务器] 行为：文本帧 echo 回发送者并广播给其他客户端；Ctrl+C 退出。");

        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                // 每个连接一个异步循环，互不阻塞
                _ = HandleClientAsync(client, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _listener.Stop();
            Console.WriteLine("[服务器] 已停止。");
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken token)
    {
        int id = Interlocked.Increment(ref _nextId);
        string remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";

        using (tcp)
        {
            NetworkStream stream = tcp.GetStream();

            // HTTP Upgrade 握手；不是 WebSocket 请求就回一段普通文本（方便用浏览器直接探活）
            if (!await HandshakeAsync(stream, token).ConfigureAwait(false)) return;

            Console.WriteLine($"[连接] #{id} {remote}");
            lock (_gate) _clients[id] = stream;

            await SendTextAsync(stream, $"[服务器] 欢迎 #{id}！发送任意文本，我会 echo 并广播给其他客户端。", token)
                .ConfigureAwait(false);
            await BroadcastAsync($"[系统] 客户端 #{id} 加入，当前在线 {OnlineCount} 个", id, token).ConfigureAwait(false);

            var buffer = new byte[8192];
            while (!token.IsCancellationRequested)
            {
                WsFrame? frame = await ReadFrameAsync(stream, buffer, token).ConfigureAwait(false);
                if (frame is null) break; // 连接断开

                switch (frame.Value.Opcode)
                {
                    case 0x1: // text
                    {
                        string text = Encoding.UTF8.GetString(frame.Value.Payload);
                        Console.WriteLine($"[#{id}] {text}");
                        await SendTextAsync(stream, $"[echo] {text}", token).ConfigureAwait(false);
                        await BroadcastAsync($"[#{id}] {text}", id, token).ConfigureAwait(false);
                        break;
                    }
                    case 0x2: // binary
                    {
                        byte[] data = frame.Value.Payload;
                        await SendTextAsync(stream, $"[服务器] 收到二进制 {data.Length} 字节（首字节 {data[0]}）", token)
                            .ConfigureAwait(false);
                        break;
                    }
                    case 0x8: // close：回一个 close 帧并结束
                        await WriteFrameAsync(stream, 0x8, [], token).ConfigureAwait(false);
                        return;
                    case 0x9: // ping → pong
                        await WriteFrameAsync(stream, 0xA, frame.Value.Payload, token).ConfigureAwait(false);
                        break;
                }
            }
        }

        lock (_gate) _clients.Remove(id);
        Console.WriteLine($"[断开] #{id} {remote}");
        await BroadcastAsync($"[系统] 客户端 #{id} 离开，当前在线 {OnlineCount} 个", id, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private int OnlineCount
    {
        get { lock (_gate) return _clients.Count; }
    }

    #region 握手

    private static async Task<bool> HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        // 读到空行（请求头结束）为止；浏览器一次把整个握手请求发来，逐字节读足够
        var request = new StringBuilder();
        var one = new byte[1];
        while (true)
        {
            int n = await stream.ReadAsync(one, token).ConfigureAwait(false);
            if (n <= 0) return false;
            request.Append((char)one[0]);
            if (request.Length > 8192) return false;
            if (request.Length >= 4 &&
                request[^4] == '\r' && request[^3] == '\n' && request[^2] == '\r' && request[^1] == '\n')
                break;
        }

        string raw = request.ToString();
        string requestLine = raw.Split("\r\n")[0];
        Console.WriteLine($"[http] {requestLine}");

        string? key = null;
        bool upgrade = false;
        foreach (string line in raw.Split("\r\n"))
        {
            if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                key = line[(line.IndexOf(':') + 1)..].Trim();
            else if (line.StartsWith("Upgrade:", StringComparison.OrdinalIgnoreCase) &&
                     line.Contains("websocket", StringComparison.OrdinalIgnoreCase))
                upgrade = true;
        }

        if (!upgrade || string.IsNullOrEmpty(key))
        {
            byte[] body = Encoding.UTF8.GetBytes(
                "WebSocket 测试服务器已就绪。请用 WebSocket 客户端连接，例如 ws://127.0.0.1:9000/\r\n");
            var head = new StringBuilder();
            head.Append("HTTP/1.1 200 OK\r\n");
            head.Append("Content-Type: text/plain; charset=utf-8\r\n");
            head.Append($"Content-Length: {body.Length}\r\n");
            head.Append("Connection: close\r\n\r\n");
            await stream.WriteAsync(Encoding.UTF8.GetBytes(head.ToString()), token).ConfigureAwait(false);
            await stream.WriteAsync(body, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
            return false;
        }

        string accept = Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(key + WebSocketGuid)));
        var response = new StringBuilder();
        response.Append("HTTP/1.1 101 Switching Protocols\r\n");
        response.Append("Upgrade: websocket\r\n");
        response.Append("Connection: Upgrade\r\n");
        response.Append($"Sec-WebSocket-Accept: {accept}\r\n\r\n");
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response.ToString()), token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
        return true;
    }

    #endregion

    #region 帧收发

    private readonly record struct WsFrame(bool Fin, int Opcode, byte[] Payload);

    /// <summary>读一帧；连接断开返回 null。</summary>
    private static async Task<WsFrame?> ReadFrameAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
    {
        if (!await ReadExactAsync(stream, buffer, 0, 2, token).ConfigureAwait(false)) return null;

        byte b0 = buffer[0];
        byte b1 = buffer[1];
        bool fin = (b0 & 0x80) != 0;
        int opcode = b0 & 0x0F;
        bool masked = (b1 & 0x80) != 0;
        long length = b1 & 0x7F;

        if (length == 126)
        {
            if (!await ReadExactAsync(stream, buffer, 0, 2, token).ConfigureAwait(false)) return null;
            length = (buffer[0] << 8) | buffer[1];
        }
        else if (length == 127)
        {
            if (!await ReadExactAsync(stream, buffer, 0, 8, token).ConfigureAwait(false)) return null;
            length = 0;
            for (int i = 0; i < 8; i++) length = (length << 8) | buffer[i];
        }

        if (length < 0 || length > MaxFrameBytes) throw new IOException($"帧过大：{length} 字节。");

        var mask = new byte[4];
        if (masked && !await ReadExactAsync(stream, mask, 0, 4, token).ConfigureAwait(false)) return null;

        var payload = new byte[length];
        if (length > 0 && !await ReadExactAsync(stream, payload, 0, (int)length, token).ConfigureAwait(false)) return null;

        // 客户端发来的帧必须带掩码，这里就地解掩码
        if (masked)
        {
            for (int i = 0; i < payload.Length; i++) payload[i] ^= mask[i & 3];
        }

        return new WsFrame(fin, opcode, payload);
    }

    /// <summary>写（服务器 → 客户端不带掩码）。</summary>
    private static async Task WriteFrameAsync(NetworkStream stream, int opcode, byte[] payload, CancellationToken token)
    {
        var head = new List<byte>(14) { (byte)(0x80 | opcode) };

        if (payload.Length < 126)
        {
            head.Add((byte)payload.Length);
        }
        else if (payload.Length <= 0xFFFF)
        {
            head.Add(126);
            head.Add((byte)(payload.Length >> 8));
            head.Add((byte)(payload.Length & 0xFF));
        }
        else
        {
            head.Add(127);
            for (int i = 7; i >= 0; i--) head.Add((byte)(((long)payload.Length >> (i * 8)) & 0xFF));
        }

        await stream.WriteAsync(head.ToArray(), token).ConfigureAwait(false);
        if (payload.Length > 0) await stream.WriteAsync(payload, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count,
                                                  CancellationToken token)
    {
        int read = 0;
        while (read < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset + read, count - read), token).ConfigureAwait(false);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    private static Task SendTextAsync(NetworkStream stream, string text, CancellationToken token)
        => WriteFrameAsync(stream, 0x1, Encoding.UTF8.GetBytes(text), token);

    private async Task BroadcastAsync(string text, int exceptId, CancellationToken token)
    {
        byte[] payload = Encoding.UTF8.GetBytes(text);

        List<NetworkStream> targets = [];
        lock (_gate)
        {
            foreach (var (id, stream) in _clients)
            {
                if (id != exceptId) targets.Add(stream);
            }
        }

        foreach (NetworkStream stream in targets)
        {
            try
            {
                await WriteFrameAsync(stream, 0x1, payload, token).ConfigureAwait(false);
            }
            catch
            {
                // 单个客户端写失败（已断开等）不影响其余
            }
        }
    }

    #endregion
}
