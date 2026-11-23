// partial 文件：Envir 类按功能拆分，方法体从原 Envir.cs 原样迁入。
using ClientPackets;
using Server.Library.MirDatabase;
using Server.Library.Utils;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using Server.MirObjects.Monsters;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using Library.Network;
using S = ServerPackets;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        public void Start()
        {
            if (Running || _thread != null) return;

            Running = true;

            _thread = new Thread(WorkLoop) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            Running = false;

            lock (_locker)
            {
                // changing a blocking condition. (this makes the threads wake up!)
                Monitor.PulseAll(_locker);
            }

            //simply intterupt all the mob threads if they are running (will give an invisible error on them but fastest way of getting rid of them on shutdowns)
            for (var i = 1; i < MobThreading.Length; i++)
            {
                if (MobThreads[i] != null)
                {
                    MobThreads[i].EndTime = Time + 9999;
                }
                if (MobThreading[i] != null &&
                    MobThreading[i].ThreadState != System.Threading.ThreadState.Stopped && MobThreading[i].ThreadState != System.Threading.ThreadState.Unstarted)
                {
                    MobThreading[i].Interrupt();
                }
            }

            http?.Stop();

            while (_thread != null)
                Thread.Sleep(1);
        }

        public void Reboot()
        {
            new Thread(() =>
            {
                MessageQueue.Enqueue("Server rebooting...");
                Stop();
                Start();
            }).Start();
        }

        public void UpdateIPBlock(string ipAddress, TimeSpan value)
        {
            IPBlocks[ipAddress] = Now.Add(value);
        }

        private void StartEnvir()
        {
            Players.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            MapList.Clear();
            GTMapList.Clear();
            GameshopLog.Clear();
            CustomCommands.Clear();
            Heroes.Clear();
            MonsterCount = 0;

            LoadExcelConfig();

            BuffInfoList.Clear();
            foreach (var buff in BuffInfo.Load())
            {
                BuffInfoList.Add(buff);
            }

            MessageQueue.Enqueue(string.Format("{0} Buffs Loaded.", BuffInfoList.Count));

            RecipeInfoList.Clear();
            foreach (var recipe in Directory.GetFiles(Settings.RecipePath, "*.txt")
                         .Select(path => Path.GetFileNameWithoutExtension(path))
                         .ToArray())
            {
                RecipeInfoList.Add(new RecipeInfo(recipe));
            }

            MessageQueue.Enqueue(string.Format("{0} Recipes Loaded.", RecipeInfoList.Count));

            for (var i = 0; i < MapInfoList.Count; i++)
            {
                // Call CreateMap(), which adds the map to Envir.MapList
                MapInfoList[i].CreateMap();

                // Fetch the created map from Envir.MapList
                Map map = MapList.FirstOrDefault(m => m.Info == MapInfoList[i]);

                if (map != null)
                {
                    if (MapInfoList[i].GT)
                    {
                        GTMap gt = GTMapList.FirstOrDefault(x => x.Index == MapInfoList[i].GTIndex);
                        if (gt != null)
                        {
                            gt.Maps.Add(map);
                        }
                        else
                        {
                            var GT = new GTMap()
                            {
                                Index = MapInfoList[i].GTIndex,
                                Name = MapInfoList[i].Title,
                                Price = Settings.BuyGTGold,
                                Days = 0,
                                Begin = 0,
                                Leader = "None",
                                Owner = "None",
                            };
                            GT.Maps.Add(map);

                            GTMapList.Add(GT);
                        }
                    }
                }
            }

            MessageQueue.Enqueue(string.Format("{0} Maps Loaded.", MapInfoList.Count));

            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i].StartItem)
                {
                    StartItems.Add(ItemInfoList[i]);
                }
            }

            ReloadDrops();

            LoadDisabledChars();
            LoadLineMessages();

            if (DragonInfoTable.Enabled)
            {
                DragonSystem = new Dragon(DragonInfoTable);
                if (DragonSystem != null)
                {
                    if (DragonSystem.Load()) DragonSystem.Info.LoadDrops();
                }

                MessageQueue.Enqueue("Dragon Loaded.");
            }

            DefaultNPC = NPCScript.GetOrAdd((uint)Random.Next(1000000, 1999999), Settings.DefaultNPCFilename, NPCScriptType.AutoPlayer);
            MonsterNPC = NPCScript.GetOrAdd((uint)Random.Next(2000000, 2999999), Settings.MonsterNPCFilename, NPCScriptType.AutoMonster);
            RobotNPC = NPCScript.GetOrAdd((uint)Random.Next(3000000, 3999999), Settings.RobotNPCFilename, NPCScriptType.Robot);

            MessageQueue.Enqueue("Envir Started.");           

        }

        
        private void StartNetwork()
        {
            Connections.Clear();

            LoadAccounts();

            LoadGuilds();

            LoadConquests();
            LoadGTInfo();

            _listener = new TcpListener(IPAddress.Parse(Settings.IPAddress), Settings.Port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(Connection, null);

            if (StatusPortEnabled)
            {
                _StatusPort = new TcpListener(IPAddress.Parse(Settings.IPAddress), 3000);
                _StatusPort.Start();
                _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }

            MessageQueue.Enqueue("Network Started.");

            // 打印 WebSocket 监听地址与端口，方便客户端直接复制连接
            if (Settings.IPAddress == "0.0.0.0" || Settings.IPAddress == "*")
                MessageQueue.Enqueue(string.Format("WebSocket 监听已启动: ws://0.0.0.0:{0}  (本机请连接 ws://127.0.0.1:{0})", Settings.Port));
            else
                MessageQueue.Enqueue(string.Format("WebSocket 监听已启动: ws://{0}:{1}", Settings.IPAddress, Settings.Port));
        }

        private void StopEnvir()
        {
            SaveGoods(true);

            MapList.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            Objects.Clear();
            Players.Clear();
            Heroes.Clear();
            GTMapList.Clear();

            CleanUp();

            GC.Collect();

            MessageQueue.Enqueue("Envir Stopped.");
        }
        private void StopNetwork()
        {
            _listener.Stop();
            lock (Connections)
            {
                for (var i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].SendDisconnect(0);
            }

            if (StatusPortEnabled)
            {
                _StatusPort.Stop();
                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].SendDisconnect();
            }

            var expire = Time + 5000;

            while (Connections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (var i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].Process();

                Thread.Sleep(1);
            }


            Connections.Clear();

            expire = Time + 10000;
            while (StatusConnections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].Process();

                Thread.Sleep(1);
            }


            StatusConnections.Clear();
            MessageQueue.Enqueue("Network Stopped.");
        }

        private void CleanUp()
        {
            for (var i = 0; i < CharacterList.Count; i++)
            {
                var info = CharacterList[i];

                if (info.Deleted)
                {
                    #region Mentor Cleanup
                    if (info.Mentor > 0)
                    {
                        var mentor = GetCharacterInfo(info.Mentor);

                        if (mentor != null)
                        {
                            mentor.Mentor = 0;
                            mentor.MentorExp = 0;
                            mentor.IsMentor = false;
                        }

                        info.Mentor = 0;
                        info.MentorExp = 0;
                        info.IsMentor = false;
                    }
                    #endregion

                    #region Marriage Cleanup
                    if (info.Married > 0)
                    {
                        var Lover = GetCharacterInfo(info.Married);

                        info.Married = 0;
                        info.MarriedDate = Now;

                        Lover.Married = 0;
                        Lover.MarriedDate = Now;
                        if (Lover.Equipment[(int)EquipmentSlot.RingL] != null)
                            Lover.Equipment[(int)EquipmentSlot.RingL].WeddingRing = -1;
                    }
                    #endregion
                }

                if (info.Mail.Count > Settings.MailCapacity)
                {
                    for (var j = info.Mail.Count - 1 - (int)Settings.MailCapacity; j >= 0; j--)
                    {
                        if (info.Mail[j].DateOpened > Now && info.Mail[j].Collected && info.Mail[j].Items.Count == 0 && info.Mail[j].Gold == 0)
                        {
                            info.Mail.Remove(info.Mail[j]);
                        }
                    }
                }
            }
        }

        private void Connection(IAsyncResult result)
        {
            try
            {
                if (!Running || !_listener.Server.IsBound) return;
            }
            catch (Exception e)
            {
                MessageQueue.Enqueue(e.ToString());
            }

            try
            {
                var tempTcpClient = _listener.EndAcceptTcpClient(result);

                var ipAddress = tempTcpClient.Client.RemoteEndPoint.ToString().Split(':')[0];

                // WebSocket 升级握手：浏览器 WASM 客户端以 ws:// 连接，这里在原始 TCP 流上完成
                // HTTP 101 握手，再用 WebSocketStream 包装成 Stream 交给 MirConnection，
                // 上层仍走原版 Mir2 字节帧（Packet.ReceivePacket / GetPacketBytes），协议不变。
                WebSocketStream webSocket = null;
                try
                {
                    webSocket = WebSocketStream.Accept(tempTcpClient.GetStream());
                }
                catch (Exception ex)
                {
                    MessageQueue.Enqueue("WebSocket 握手失败: " + ex.Message);
                    tempTcpClient.Close();
                    webSocket = null;
                }

                if (webSocket != null)
                {
                    if (!IPBlocks.TryGetValue(ipAddress, out DateTime banDate) || banDate < Now)
                    {
                        int count = 0;

                        for (int i = 0; i < Connections.Count; i++)
                        {
                            var connection = Connections[i];

                            if (!connection.Connected || connection.IPAddress != ipAddress)
                                continue;

                            count++;
                        }

                        if (count >= Settings.MaxIP)
                        {
                            UpdateIPBlock(ipAddress, TimeSpan.FromSeconds(Settings.IPBlockSeconds));

                            MessageQueue.Enqueue(string.Format("{0} Disconnected, Too many connections.", ipAddress));
                            tempTcpClient.Close();
                        }
                        else
                        {
                            var tempConnection = new MirConnection(++_sessionID, webSocket, ipAddress);
                            if (tempConnection.Connected)
                            {
                                lock (Connections)
                                    Connections.Add(tempConnection);
                            }
                        }
                    }
                    else
                    {
                        tempTcpClient.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
            finally
            {
                while (Connections.Count >= Settings.MaxUser)
                    Thread.Sleep(1);

                if (Running && _listener.Server.IsBound)
                    _listener.BeginAcceptTcpClient(Connection, null);
            }
        }

        private void StatusConnection(IAsyncResult result)
        {
            if (!Running || !_StatusPort.Server.IsBound) return;

            try
            {
                var tempTcpClient = _StatusPort.EndAcceptTcpClient(result);
                lock (StatusConnections)
                    StatusConnections.Add(new MirStatusConnection(tempTcpClient));
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
            finally
            {
                while (StatusConnections.Count >= 5) //dont allow to many status port connections it's just an abuse thing
                    Thread.Sleep(1);

                if (Running && _StatusPort.Server.IsBound)
                    _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }
        }

    }
}
