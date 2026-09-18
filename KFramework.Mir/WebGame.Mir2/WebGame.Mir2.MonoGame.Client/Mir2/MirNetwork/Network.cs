using System.Collections.Concurrent;
using Client.MirControls;
using C = ClientPackets;
using MirEngine;

namespace Client.MirNetwork
{
    // 浏览器版 Network：用 BrowserWebSocket 取代 TcpClient。每帧 Process() 轮询收包。
    static class Network
    {
        private static int _ws;
        public static int ConnectAttempt = 0;
        public static int MaxAttempts = 20;
        public static bool ErrorShown;
        public static bool Connected;
        public static long TimeOutTime, TimeConnected, RetryTime = CMain.Time + 5000;

        private static ConcurrentQueue<Packet> _receiveList;
        private static ConcurrentQueue<Packet> _sendList = new ConcurrentQueue<Packet>();

        public static void Connect()
        {
            if (_ws > 0) Disconnect();

            if (ConnectAttempt >= MaxAttempts)
            {
                if (ErrorShown) return;
                ErrorShown = true;
                MirMessageBox errorBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ErrorConnectingToServer), MirMessageBoxButtons.Cancel);
                errorBox.CancelButton.Click += (o, e) => Program.Form.Close();
                errorBox.Label.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MaximumConnectionAttemptsReached), MaxAttempts);
                errorBox.Show();
                return;
            }

            ConnectAttempt++;

            try
            {
                string url = $"ws://{Settings.IPAddress}:{Settings.Port}";
                _ws = BrowserWebSocket.Connect(url);
                if (_ws > 0)
                {
                    _receiveList = new ConcurrentQueue<Packet>();
                    TimeOutTime = CMain.Time + Settings.TimeOut;
                    TimeConnected = CMain.Time;
                }
                else
                {
                    Thread.Sleep(100);
                    Connect();
                }
            }
            catch (System.Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
                Disconnect();
            }
        }

        public static void Disconnect()
        {
            if (_ws > 0) { BrowserWebSocket.Close(_ws); _ws = 0; }
            TimeConnected = 0;
            Connected = false;
            _sendList = new ConcurrentQueue<Packet>();
            _receiveList = null;
        }

        public static void Process()
        {
            if (_ws <= 0)
            {
                if (Connected)
                {
                    while (_receiveList != null && !_receiveList.IsEmpty)
                    {
                        if (!_receiveList.TryDequeue(out Packet p) || p == null) continue;
                        if (!(p is ServerPackets.Disconnect) && !(p is ServerPackets.ClientVersion)) continue;
                        MirScene.ActiveScene.ProcessPacket(p);
                        _receiveList = null;
                        return;
                    }
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.LostConnectionWithServer), true);
                    Disconnect();
                    return;
                }
                else if (CMain.Time >= RetryTime)
                {
                    RetryTime = CMain.Time + 5000;
                    Connect();
                }
                return;
            }

            int state = BrowserWebSocket.GetState(_ws);
            if (state == 3) // CLOSED
            {
                Disconnect();
                Connect();
                return;
            }

            if (!Connected && state == 1) // OPEN
            {
                Connected = true;
            }

            // 收取所有已到达的消息。一条 WS 二进制消息可能包含多个完整 Mir Packet：
            // 服务器 MirConnection.BeginSend 会把 _sendList 里多个 Packet 的字节拼成一个 buffer 一次 Write，
            // WebSocketStream 以 endOfMessage=true 发出，于是“一条 WS 消息 = 多个 Packet”。
            // 必须像 TCP 端（MirConnection.cs 的 ReceiveData）那样循环切分，否则只解析第一条、其余全部丢失——
            // 典型表现就是登录框卡在“尝试连接服务器”：S.Connected 被解析后回了版本，但同一条消息里的
            // S.ClientVersion 被丢弃，客户端永远等不到回应。
            byte[] msg;
            while (_receiveList != null && (msg = BrowserWebSocket.Receive(_ws)).Length > 0)
            {
                byte[] rest = msg;
                Packet p;
                while (rest != null && rest.Length > 0)
                {
                    p = Packet.ReceivePacket(rest, out rest);
                    if (p == null) break;   // 该消息内无更多完整 Packet（损坏/截断），丢弃剩余
                    _receiveList.Enqueue(p);
                }
            }

            while (_receiveList != null && !_receiveList.IsEmpty)
            {
                if (!_receiveList.TryDequeue(out Packet p) || p == null) continue;
                MirScene.ActiveScene.ProcessPacket(p);
            }

            if (CMain.Time > TimeOutTime && _sendList != null && _sendList.IsEmpty)
                _sendList.Enqueue(new C.KeepAlive());

            if (_sendList == null || _sendList.IsEmpty) return;

            TimeOutTime = CMain.Time + Settings.TimeOut;

            var data = new List<byte>();
            while (!_sendList.IsEmpty)
            {
                if (!_sendList.TryDequeue(out Packet p) || p == null) continue;
                data.AddRange(p.GetPacketBytes());
            }

            CMain.BytesSent += data.Count;
            BrowserWebSocket.Send(_ws, data.ToArray());
        }

        public static void Enqueue(Packet p)
        {
            if (_sendList != null && p != null)
                _sendList.Enqueue(p);
        }
    }
}
