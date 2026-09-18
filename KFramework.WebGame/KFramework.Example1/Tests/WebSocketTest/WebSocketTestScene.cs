using System.Diagnostics;
using System.Text;

using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.WebSocketTest;

/// <summary>
/// WebSocket 测试（只做客户端）：用浏览器原生 WebSocket（<see cref="Net_WebSocket_Client"/>）
/// 连接自写的本地测试服务器 <c>Tools/WebSocketTestServer</c>（默认 <c>ws://127.0.0.1:9000/</c>）。
///
/// <para>服务器行为：收到文本帧 → 给发送者回一条 <c>[echo]</c>，同时广播给其他在线客户端；
/// 收到二进制帧 → 回一条长度说明；ping → pong。</para>
///
/// <para>用法：先 <c>dotnet run --project Tools/WebSocketTestServer</c> 起服务器，
/// 再在本页点「连接」，输入字母 / 数字后按 Enter 发送（中文请用「发送 中文消息」按钮）。</para>
/// </summary>
public sealed class WebSocketTestScene : TestSceneBase
{
    public override string Title => "WebSocket 测试（客户端）";

    /// <summary>测试服务器地址（与 Tools/WebSocketTestServer 的监听端口保持一致）。</summary>
    private const string ServerUrl = "ws://127.0.0.1:9000/";

    private static readonly string[] ButtonNames =
        ["连接", "断开", "发送输入框", "发送 Hello", "发送 中文消息", "收 256KB", "收 1MB", "清空日志"];

    private readonly List<Rectangle> _buttons = [];
    private readonly List<string> _log = [];

    /// <summary>
    /// 按钮行的起始 y：由 DrawBody 的流式布局算出来（③ 操作 标题之后），Update 的 Layout() 复用它，
    /// 保证点击命中区与画面上看到的位置一致（别再写死，否则会跟上面的 ①② 区重叠）。
    /// 初值只在首帧 Draw 之前兜底。
    /// </summary>
    private float _buttonsTop = 190f;

    private Net_WebSocket_Client? _ws;
    private string _status = "未连接";
    private string _input = "";

    public override void LoadContent() => Input_KeyBoard.KeyDown += OnKeyDown;

    public override void Dispose()
    {
        Input_KeyBoard.KeyDown -= OnKeyDown;
        _ws?.Close();
        _ws = null;
        base.Dispose();
    }

    public override void Update()
    {
        base.Update();
        // 已切回总屏（Esc / 返回）就别再处理本页点击
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

        Layout();

        if (Input_Mouse.GetButtonDown(MouseButton.Left))
        {
            Vector2 p = Input_Mouse.Position;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Contains(p))
                {
                    OnButton(i);
                    return;
                }
            }
        }
    }

    private void OnButton(int index)
    {
        switch (index)
        {
            case 0: _ = ConnectAsync(); break;
            case 1: _ws?.Close(); _status = "已主动断开"; Log("[断开] 客户端主动关闭"); break;
            case 2: Send(_input, clearInput: true); break;
            case 3: Send("Hello WebSocket " + DateTime.Now.ToString("HH:mm:ss")); break;
            case 4: Send("中文消息测试 你好，服务器！"); break;
            // 大包：让服务器下发 > 64KB 的二进制帧，验证零拷贝通道（Net_RecvBuffer + MemoryView 写入）
            case 5: Send("big " + (256 * 1024)); break;
            case 6: Send("big " + (1024 * 1024)); break;
            case 7: _log.Clear(); break;
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            _ws?.Close();
            var ws = new Net_WebSocket_Client();
            ws.Opened += () => { _status = "已连接 " + ServerUrl; Log("[打开] " + ServerUrl); };
            ws.Closed += code => { _status = $"已关闭（code={code}）"; Log("[关闭] code=" + code); };
            ws.Error += message => { _status = "错误：" + message; Log("[错误] " + message); };
            ws.MessageReceived += data => OnReceived(ws, data);
            _ws = ws;

            _status = "连接中…";
            Log("[连接] " + ServerUrl);
            await ws.ConnectAsync(ServerUrl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _status = "连接失败：" + ex.Message;
            Log("[异常] " + ex.Message);
        }
    }

    private void Send(string? text, bool clearInput = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_ws is null || !_ws.IsOpen)
        {
            Log("[提示] 还没连上，先点「连接」");
            return;
        }

        _ws.Send(Encoding.UTF8.GetBytes(text));
        Log("[发送] " + text);
        if (clearInput) _input = "";
    }

    private void OnKeyDown(Keys key)
    {
        if (key == Keys.Backspace)
        {
            if (_input.Length > 0) _input = _input[..^1];
            return;
        }
        if (key == Keys.Enter)
        {
            Send(_input, clearInput: true);
            return;
        }
        if (key == Keys.Space)
        {
            _input += " ";
            return;
        }

        // 浏览器的 KeyboardEvent.keyCode 与本枚举的字母 / 数字同码（无输入法，中文走预设按钮）
        char c = (char)(int)key;
        if (char.IsAsciiLetterOrDigit(c)) _input += c;
    }

    private void Layout()
    {
        _buttons.Clear();

        int x = 28;
        int y = (int)_buttonsTop;
        for (int i = 0; i < ButtonNames.Length; i++)
        {
            int width = 140;
            _buttons.Add(new Rectangle(x, y, width, 40));
            x += width + 12;
            if (x + width > Device.Viewport.Width - 28)
            {
                x = 28;
                y += 52;
            }
        }
    }

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        float x = origin.X;
        float y = origin.Y;

        y += DrawSection(batch, "① 连接状态", new Vector2(x, y));
        y += DrawLine(batch, Font, "服务器：" + ServerUrl, new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "状态：" + _status, new Vector2(x, y),
                      _ws is { IsOpen: true } ? new Color(140, 255, 170) : Color.Orange);
        y += DrawLine(batch, Font, "提示：先在 Tools/WebSocketTestServer 目录执行 dotnet run 启动服务器。",
                      new Vector2(x, y), Color.DarkGray);

        // 输入框
        y += 16f;
        y += DrawSection(batch, "② 输入框（字母 / 数字 / 空格 / 退格，Enter 发送）", new Vector2(x, y));
        var inputRect = new Rectangle((int)x, (int)y, Math.Min(520, Device.Viewport.Width - 56), (int)Font.LineSpacing + 12);
        DrawRect(batch, inputRect, new Color(22, 30, 46));
        DrawRect(batch, new Rectangle(inputRect.X, inputRect.Bottom, inputRect.Width, 2), new Color(72, 150, 230));
        string shown = _input + (DateTime.Now.Second % 2 == 0 ? "_" : " ");
        batch.DrawString(Font, shown, new Vector2(inputRect.X + 8, inputRect.Y + 6), Color.White);
        y += inputRect.Height + 16f;

        // 按钮：起点跟随上面的流式布局（Layout 用它生成命中矩形，画面与点击位置才对得上）
        y += DrawSection(batch, "③ 操作", new Vector2(x, y));
        _buttonsTop = y;
        for (int i = 0; i < _buttons.Count; i++)
        {
            Rectangle rect = _buttons[i];
            bool hover = rect.Contains(Input_Mouse.Position);
            DrawRect(batch, rect, hover ? new Color(44, 70, 116) : new Color(28, 36, 56));
            batch.DrawString(Font, ButtonNames[i], new Vector2(rect.X + 12, rect.Y + 8), Color.White);
        }
        y = _buttons.Count > 0 ? _buttons[^1].Bottom + 24f : y;

        // 日志
        y += DrawSection(batch, "④ 收发日志（最近 12 条）", new Vector2(x, y));
        int start = Math.Max(0, _log.Count - 12);
        for (int i = start; i < _log.Count; i++)
            y += DrawLine(batch, Font, _log[i], new Vector2(x, y), Color.LightGray);
    }

    private void OnReceived(Net_WebSocket_Client ws, ReadOnlyMemory<byte> data)
    {
        // 大包用的是 Net_RecvBuffer 的共享固定缓冲，只在本次回调内有效 —— 这里立刻校验，不留存
        if (ws.LastMessageFromSharedBuffer)
        {
            Log(VerifyBigPacket(data.Span));
            return;
        }

        Log("[收到] " + Encoding.UTF8.GetString(data.Span));
    }

    /// <summary>校验 big 命令下发的二进制大包：服务器按 <c>i % 251</c> 填充，逐字节比对即可确认零拷贝没写错位。</summary>
    private static string VerifyBigPacket(ReadOnlySpan<byte> data)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int bad = -1;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != (byte)(i % 251))
            {
                bad = i;
                break;
            }
        }
        sw.Stop();

        return bad < 0
            ? $"[大包] {data.Length} 字节，零拷贝共享缓冲，内容校验 OK（遍历 {sw.Elapsed.TotalMilliseconds:F2} ms）"
            : $"[大包] {data.Length} 字节，内容校验失败：第 {bad} 字节不符！";
    }

    private void Log(string message)
    {
        Console.WriteLine($"[WsTest] {message}");
        _log.Add(message);
        if (_log.Count > 200) _log.RemoveAt(0);
    }
}
