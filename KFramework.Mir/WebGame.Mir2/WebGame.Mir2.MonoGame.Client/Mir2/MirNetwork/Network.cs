using System.Collections.Concurrent;
using Client.MirControls;
using C = ClientPackets;
using KFramework.MonoGame;
using MirEngine;

namespace Client.MirNetwork
{
    // 浏览器版 Network：用 KFramework.MonoGame 的 Net_WebSocket_Client 取代旧的 BrowserWebSocket。
    // 事件驱动：OnBinaryMessage 把一帧原始字节入队，Process() 每帧循环切分成 Mir Packet 并派发；
    // 发送队列合并后一次性写出（一条 WS 消息可能含多个 Packet，必须像 TCP 端那样切分）。
    static class Network
    {
        private static Net_WebSocket_Client _ws;
        public static int ConnectAttempt = 0;
        public static int MaxAttempts = 20;
        public static bool ErrorShown;
        public static bool Connected;
        public static long TimeOutTime, TimeConnected, RetryTime = CMain.Time + 5000;

        private static ConcurrentQueue<Packet> _receiveList = new ConcurrentQueue<Packet>();
        private static ConcurrentQueue<Packet> _sendList = new ConcurrentQueue<Packet>();
        private static readonly ConcurrentQueue<byte[]> _rawQueue = new ConcurrentQueue<byte[]>();

        public static void Connect()
        {
            if (_ws != null) Disconnect();

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
                _ws = new Net_WebSocket_Client();
                _ws.Opened += () => { Connected = true; TimeConnected = CMain.Time; };
                _ws.Closed += code => { Connected = false; };
                _ws.Error += msg => { if (Settings.LogErrors) CMain.SaveError(msg); };
                _ws.MessageReceived += m =>
                {
                    byte[] data = m.ToArray();
                    if (data.Length > 0) _rawQueue.Enqueue(data);
                };
                _ws.Connect(url);
                TimeOutTime = CMain.Time + Settings.TimeOut;
            }
            catch (System.Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
                Disconnect();
            }
        }

        public static void Disconnect()
        {
            if (_ws != null) { _ws.Close(); _ws = null; }
            TimeConnected = 0;
            Connected = false;
            _sendList = new ConcurrentQueue<Packet>();
            _receiveList = new ConcurrentQueue<Packet>();
            while (_rawQueue.TryDequeue(out _)) { }
        }

        public static void Process()
        {
            if (_ws == null)
            {
                if (Connected)
                {
                    while (!_receiveList.IsEmpty)
                    {
                        if (!_receiveList.TryDequeue(out Packet p) || p == null) continue;
                        if (!(p is ServerPackets.Disconnect) && !(p is ServerPackets.ClientVersion)) continue;
                        MirScene.ActiveScene.ProcessPacket(p);
                        _receiveList = new ConcurrentQueue<Packet>();
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

            if (!Connected && _ws.IsOpen)
            {
                Connected = true;
            }

            // 收取所有已到达的二进制消息并切分为 Packet（一条 WS 消息可能含多个完整 Packet）。
            byte[] msg;
            while (_rawQueue.TryDequeue(out msg))
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

            while (!_receiveList.IsEmpty)
            {
                if (!_receiveList.TryDequeue(out Packet p) || p == null) continue;
                MirScene.ActiveScene.ProcessPacket(p);
            }

            if (CMain.Time > TimeOutTime && _sendList.IsEmpty)
                _sendList.Enqueue(new C.KeepAlive());

            if (_sendList.IsEmpty) return;

            TimeOutTime = CMain.Time + Settings.TimeOut;

            var data = new List<byte>();
            while (!_sendList.IsEmpty)
            {
                if (!_sendList.TryDequeue(out Packet p) || p == null) continue;
                data.AddRange(p.GetPacketBytes());
            }

            CMain.BytesSent += data.Count;
            _ws.Send(data.ToArray());
        }

        public static void Enqueue(Packet p)
        {
            if (_sendList != null && p != null)
                _sendList.Enqueue(p);
        }
    }
}
