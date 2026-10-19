using Client.MirControls;
using KFramework.MonoGame;
using System.Collections.Concurrent;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

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
        private static readonly NetBufferCache receive_byte_list = new NetBufferCache(1024, 1024 * 64);
        private static readonly NetBufferCache send_byte_list = new NetBufferCache(1024, 1024 * 64);

        public static void Connect()
        {
            if (_ws != null) Disconnect();

            if (ConnectAttempt >= MaxAttempts)
            {
                if (ErrorShown) return;
                ErrorShown = true;
                MirMessageBox errorBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ErrorConnectingToServer), MirMessageBoxButtons.Cancel);
                errorBox.CancelButton.Click += (o, e) => CMain.Instance.Close();
                errorBox.Label.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MaximumConnectionAttemptsReached), MaxAttempts);
                errorBox.Show();
                return;
            }

            ConnectAttempt++;

            try
            {
                string url = RemoteWebSetting.GameServerUrl;
                _ws = new Net_WebSocket_Client();
                _ws.Opened += OnNet_OpenFunc;
                _ws.Closed += OnNet_ClosedFunc;
                _ws.Error += OnNet_ErrorFunc;
                _ws.MessageReceived += OnNet_MessageReceivedFunc;
                _ws.Connect(url);
                TimeOutTime = CMain.Time + Settings.TimeOut;
            }
            catch (System.Exception ex)
            {
                OnNet_ErrorFunc(ex.Message);
            }
        }

        private static void OnNet_OpenFunc()
        {
            Connected = true; TimeConnected = CMain.Time;
        }

        private static void OnNet_ClosedFunc(int code)
        {
            Connected = false;
        }

        private static void OnNet_ErrorFunc(string msg)
        {
            if (Settings.LogErrors)
            {
                CMain.SaveError(msg);
            }
            Disconnect();
        }

        private static void OnNet_MessageReceivedFunc(ReadOnlySpan<byte> m)
        {
            receive_byte_list.WriteFrom(m);
        }

        public static void Disconnect()
        {
            if (_ws != null) { _ws.Close(); _ws = null; }
            TimeConnected = 0;
            Connected = false;
            _sendList = new ConcurrentQueue<Packet>();
            _receiveList = new ConcurrentQueue<Packet>();
            receive_byte_list.Clear();
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

            while (receive_byte_list.Length > 0)
            {
                Packet p = Packet.ReceivePacket(receive_byte_list);
                if (p == null) break;   // 该消息内无更多完整 Packet（损坏/截断），丢弃剩余
                _receiveList.Enqueue(p);
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

            send_byte_list.Clear();
            while (!_sendList.IsEmpty)
            {
                if (!_sendList.TryDequeue(out Packet p) || p == null)
                {
                    continue;
                }
                ReadOnlySpan<byte> packet_byte_data = p.GetPacketBytes();
                send_byte_list.WriteFrom(packet_byte_data);
            }

            CMain.BytesSent += send_byte_list.Length;
            _ws.Send(send_byte_list.GetSpan());
        }

        public static void Enqueue(Packet p)
        {
            if (_sendList != null && p != null)
                _sendList.Enqueue(p);
        }
    }
}
