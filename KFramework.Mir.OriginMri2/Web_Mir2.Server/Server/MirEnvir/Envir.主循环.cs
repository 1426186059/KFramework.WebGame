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
        static Envir()
        {
            AccountIDReg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinAccountIDLength + "," + Globals.MaxAccountIDLength + "}$");
            PasswordReg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");
            EMailReg = new Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*");
            CharacterReg = new Regex(@"^[\u4e00-\u9fa5_A-Za-z0-9]{" + Globals.MinCharacterNameLength + "," + Globals.MaxCharacterNameLength + "}$");
        }

        public static bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;

            return PasswordReg.IsMatch(password);
        }

        public static int LastCount = 0, LastRealCount = 0;
        public static long LastRunTime = 0;
        public int MonsterCount;

        private long warTime, guildTime, conquestTime, rentalItemsTime, auctionTime, spawnTime, robotTime, timerTime;
        private int dailyTime = DateTime.UtcNow.Day;
        // 旧 Mir2 硬编码技能兜底（MagicExists / UpdateMagicInfo / FillMagicInfoList）已整体移除。
        // 技能表完全由构建期 Mir2SkillConverter 从原始传奇 5config.skill 生成，服务器不再注入旧技能。

        private string CanStartEnvir()
        {
            if (StartPoints.Count == 0) return LanguageMgr.Get("CannotStartServerWithoutMapAndStartPoint");

            if (Settings.EnforceDBChecks)
            {
                if (GetMonsterInfo(Settings.SkeletonName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.SkeletonName;
                if (GetMonsterInfo(Settings.ShinsuName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.ShinsuName;
                if (GetMonsterInfo(Settings.BugBatName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BugBatName;
                if (GetMonsterInfo(Settings.Zuma1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Zuma1;
                if (GetMonsterInfo(Settings.Zuma2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Zuma2;
                if (GetMonsterInfo(Settings.Zuma3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Zuma3;
                if (GetMonsterInfo(Settings.Zuma4, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Zuma4;
                if (GetMonsterInfo(Settings.Zuma5, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Zuma5;
                if (GetMonsterInfo(Settings.Zuma6, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Zuma6;
                if (GetMonsterInfo(Settings.Zuma7, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Zuma7;
                if (GetMonsterInfo(Settings.Turtle1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Turtle1;
                if (GetMonsterInfo(Settings.Turtle2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Turtle2;
                if (GetMonsterInfo(Settings.Turtle3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Turtle3;
                if (GetMonsterInfo(Settings.Turtle4, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Turtle4;
                if (GetMonsterInfo(Settings.Turtle5, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.Turtle5;
                if (GetMonsterInfo(Settings.BoneMonster1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BoneMonster1;
                if (GetMonsterInfo(Settings.BoneMonster2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BoneMonster2;
                if (GetMonsterInfo(Settings.BoneMonster3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BoneMonster3;
                if (GetMonsterInfo(Settings.BoneMonster4, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BoneMonster4;
                if (GetMonsterInfo(Settings.BehemothMonster1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BehemothMonster1;
                if (GetMonsterInfo(Settings.BehemothMonster2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BehemothMonster2;
                if (GetMonsterInfo(Settings.BehemothMonster3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BehemothMonster3;
                if (GetMonsterInfo(Settings.HellKnight1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HellKnight1;
                if (GetMonsterInfo(Settings.HellKnight2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HellKnight2;
                if (GetMonsterInfo(Settings.HellKnight3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HellKnight3;
                if (GetMonsterInfo(Settings.HellKnight4, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HellKnight4;
                if (GetMonsterInfo(Settings.HellBomb1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HellBomb1;
                if (GetMonsterInfo(Settings.HellBomb2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HellBomb2;
                if (GetMonsterInfo(Settings.HellBomb3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HellBomb3;
                if (GetMonsterInfo(Settings.WhiteSnake, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.WhiteSnake;
                if (GetMonsterInfo(Settings.AngelName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.AngelName;
                if (GetMonsterInfo(Settings.BombSpiderName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.BombSpiderName;
                if (GetMonsterInfo(Settings.CloneName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.CloneName;
                if (GetMonsterInfo(Settings.AssassinCloneName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.AssassinCloneName;
                if (GetMonsterInfo(Settings.VampireName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.VampireName;
                if (GetMonsterInfo(Settings.ToadName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.ToadName;
                if (GetMonsterInfo(Settings.SnakeTotemName, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.SnakeTotemName;
                if (GetMonsterInfo(Settings.FishingMonster, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.FishingMonster;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.GeneralMeowMeowMob1;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.GeneralMeowMeowMob2;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.GeneralMeowMeowMob3;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob4, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.GeneralMeowMeowMob4;
                if (GetMonsterInfo(Settings.KingHydraxMob, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.KingHydraxMob;
                if (GetMonsterInfo(Settings.HornedCommanderMob, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HornedCommanderMob;
                if (GetMonsterInfo(Settings.HornedCommanderBombMob, true) == null)
                    return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.HornedCommanderBombMob;
                if (GetMonsterInfo(Settings.SnowWolfKingMob, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.SnowWolfKingMob;
                if (GetMonsterInfo(Settings.ScrollMob1, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.ScrollMob1;
                if (GetMonsterInfo(Settings.ScrollMob2, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.ScrollMob2;
                if (GetMonsterInfo(Settings.ScrollMob3, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.ScrollMob3;
                if (GetMonsterInfo(Settings.ScrollMob4, true) == null) return LanguageMgr.Get("CannotStartServerWithoutMob")+ Settings.ScrollMob4;

                if (GetItemInfo(Settings.RefineOreName) == null) return LanguageMgr.Get("CannotStartServerWithoutItem")+ Settings.RefineOreName;
            }

            WorldMapIcon wmi = ValidateWorldMap();
            if (wmi != null)
                return LanguageMgr.Get("InvalidWorldmapIndex", wmi.MapIndex, wmi.Title);


            //add intelligent creature checks?

            return "true";
        }

        private void WorkLoop()
        {
            try
            {
                Time = Stopwatch.ElapsedMilliseconds;

                var conTime = Time;
                var saveTime = Time + Settings.SaveDelay * Settings.Minute;
                var userTime = Time + Settings.Minute * 5;
                var lineMessageTime = Time + Settings.Minute * Settings.LineMessageTimer;
                var processTime = Time + 1000;
                var startTime = Time;

                var processCount = 0;
                var processRealCount = 0;

                LinkedListNode<MapObject> current = null;

                if (Settings.Multithreaded)
                {
                    for (var j = 0; j < MobThreads.Length; j++)
                    {
                        MobThreads[j] = new MobThread();
                        MobThreads[j].Id = j;
                    }
                }

                StartEnvir();
                var canstartserver = CanStartEnvir();
                if (canstartserver != "true")
                {
                    MessageQueue.Enqueue(canstartserver);
                    StopEnvir();
                    _thread = null;
                    Stop();
                    return;
                }

                if (Settings.Multithreaded)
                {
                    for (var j = 0; j < MobThreads.Length; j++)
                    {
                        var Info = MobThreads[j];
                        if (j <= 0) continue;
                        MobThreading[j] = new Thread(() => ThreadLoop(Info)) { IsBackground = true };
                        MobThreading[j].Start();
                    }
                }

                StartNetwork();
                if (Settings.StartHTTPService)
                {
                    http = new HttpServer();
                    http.Start();
                }
                try
                {
                    while (Running)
                    {
                        Time = Stopwatch.ElapsedMilliseconds;

                        if (Time >= processTime)
                        {
                            LastCount = processCount;
                            LastRealCount = processRealCount;
                            processCount = 0;
                            processRealCount = 0;
                            processTime = Time + 1000;
                        }

                        if (conTime != Time)
                        {
                            conTime = Time;

                            AdjustLights();

                            lock (Connections)
                            {
                                for (var i = Connections.Count - 1; i >= 0; i--)
                                {
                                    Connections[i].Process();
                                }
                            }

                            lock (StatusConnections)
                            {
                                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                                {
                                    StatusConnections[i].Process();
                                }
                            }
                        }


                        if (current == null)
                            current = Objects.First;

                        if (current == Objects.First)
                        {
                            LastRunTime = Time - startTime;
                            startTime = Time;
                        }

                        if (Settings.Multithreaded)
                        {
                            for (var j = 1; j < MobThreads.Length; j++)
                            {
                                var Info = MobThreads[j];

                                if (!Info.Stop) continue;
                                Info.EndTime = Time + 10;
                                Info.Stop = false;
                            }
                            lock (_locker)
                            {
                                Monitor.PulseAll(_locker); //changing a blocking condition. (this makes the threads wake up!)
                            }
                            //run the first loop in the main thread so the main thread automaticaly 'halts' until the other threads are finished
                            ThreadLoop(MobThreads[0]);
                        }

                        var TheEnd = false;
                        var Start = Stopwatch.ElapsedMilliseconds;
                        while (!TheEnd && Stopwatch.ElapsedMilliseconds - Start < 20)
                        {
                            if (current == null)
                            {
                                TheEnd = true;
                                break;
                            }

                            var next = current.Next;
                            if (!Settings.Multithreaded || current.Value.Race != ObjectType.Monster || current.Value.Master != null)
                            {
                                if (Time > current.Value.OperateTime)
                                {
                                    current.Value.Process();
                                    current.Value.SetOperateTime();
                                }
                                processCount++;
                            }
                            current = next;
                        }

                        for (var i = 0; i < MapList.Count; i++)
                            MapList[i].Process();

                        DragonSystem?.Process();

                        Process();

                        if (Time >= saveTime)
                        {
                            saveTime = Time + Settings.SaveDelay * Settings.Minute;
                            BeginSaveAccounts();
                            SaveGuilds();
                            SaveGoods();
                            SaveConquests();
                        }

                        if (Time >= userTime)
                        {
                            userTime = Time + Settings.Minute * 5;
                            Broadcast(new S.Chat
                            {
                                Message = LanguageMgr.Get("OnlinePlayers", Players.Count),
                                Type = ChatType.System
                            });
                        }

                        if (LineMessages.Count > 0 && Time >= lineMessageTime)
                        {
                            lineMessageTime = Time + Settings.Minute * Settings.LineMessageTimer;
                            Broadcast(new S.Chat
                            {
                                Message = LineMessages[Random.Next(LineMessages.Count)],
                                Type = ChatType.System
                            });
                        }

                        //   if (Players.Count == 0) Thread.Sleep(1);
                        //   GC.Collect();
                    }
                }
                catch (Exception ex)
                {
                    lock (Connections)
                    {
                        for (var i = Connections.Count - 1; i >= 0; i--)
                            Connections[i].SendDisconnect(3);
                    }

                    // Get stack trace for the exception with source file information
                    var st = new StackTrace(ex, true);
                    // Get the top stack frame
                    var frame = st.GetFrame(0);
                    // Get the line number from the stack frame
                    var line = frame.GetFileLineNumber();

                    MessageQueue.Enqueue(string.Format("[inner workloop error. Line {0}]{1}", line, ex));
                }

                StopNetwork();
                StopEnvir();
                SaveAccounts();
                SaveGuilds(true);
                SaveConquests(true);
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();

                MessageQueue.Enqueue(string.Format("[outer workloop error. Line {0}]{1}", line, ex));
            }

            _thread = null;
        }

        private void ThreadLoop(MobThread Info)
        {
            Info.Stop = false;

            try
            {
                var stopping = false;
                if (Info._current == null)
                    Info._current = Info.ObjectsList.First;
                stopping = Info._current == null;

                while (Running)
                {
                    if (Info._current == null)
                        Info._current = Info.ObjectsList.First;
                    else
                    {
                        var next = Info._current.Next;

                        //if we reach the end of our list > go back to the top (since we are running threaded, we dont want the system to sit there for xxms doing nothing)
                        if (Info._current == Info.ObjectsList.Last)
                        {
                            next = Info.ObjectsList.First;
                            Info.LastRunTime = (Info.LastRunTime + (Time - Info.StartTime)) / 2;
                            //Info.LastRunTime = (Time - Info.StartTime) /*> 0 ? (Time - Info.StartTime) : Info.LastRunTime */;
                            Info.StartTime = Time;
                        }
                        if (Time > Info._current.Value.OperateTime)
                        {
                            if (Info._current.Value.Master == null) //since we are running multithreaded, dont allow pets to be processed (unless you constantly move pets into their map appropriate thead)
                            {
                                Info._current.Value.Process();
                                Info._current.Value.SetOperateTime();
                            }
                        }
                        Info._current = next;
                    }

                    //if it's the main thread > make it loop till the subthreads are done, else make it stop after 'endtime'
                    if (Info.Id == 0)
                    {
                        stopping = true;
                        for (var x = 1; x < MobThreads.Length; x++)
                        {
                            if (MobThreads[x].Stop == false)
                            {
                                stopping = false;
                            }
                        }
                        if (!stopping) continue;
                        Info.Stop = stopping;
                        return;
                    }

                    if (Stopwatch.ElapsedMilliseconds <= Info.EndTime || !Running) continue;
                    Info.Stop = true;
                    lock (_locker)
                    {
                        while (Info.Stop) Monitor.Wait(_locker);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ThreadInterruptedException) return;

                MessageQueue.Enqueue(string.Format("[threadloop error]{0}", ex));
            }
        }

        private void AdjustLights()
        {
            var oldLights = Lights;

            var hours = Now.Hour * 2 % 24;
            if (hours == 6 || hours == 7)
                Lights = LightSetting.Dawn;
            else if (hours >= 8 && hours <= 15)
                Lights = LightSetting.Day;
            else if (hours == 16 || hours == 17)
                Lights = LightSetting.Evening;
            else
                Lights = LightSetting.Night;

            if (oldLights == Lights) return;

            Broadcast(new S.TimeOfDay { Lights = Lights });
        }

        public void Process()
        {
            if (Now.Day != dailyTime)
            {
                dailyTime = Now.Day;
                ProcessNewDay();
            }

            if (Time >= warTime)
            {
                warTime = Time + Settings.Minute;
                for (var i = GuildsAtWar.Count - 1; i >= 0; i--)
                {
                    GuildsAtWar[i].TimeRemaining -= Settings.Minute;

                    if (GuildsAtWar[i].TimeRemaining >= 0) continue;
                    GuildsAtWar[i].EndWar();
                    GuildsAtWar.RemoveAt(i);
                }
            }

            if (Time >= guildTime)
            {
                guildTime = Time + Settings.Minute;
                for (var i = 0; i < Guilds.Count; i++)
                {
                    Guilds[i].Process();
                }
            }

            if (Time >= conquestTime)
            {
                conquestTime = Time + Settings.Second * 10;
                for (var i = 0; i < Conquests.Count; i++)
                {
                    Conquests[i].Process();
                }
            }

            if (Time >= rentalItemsTime)
            {
                rentalItemsTime = Time + Settings.Minute * 5;
                ProcessRentedItems();
            }

            if (Time >= auctionTime)
            {
                auctionTime = Time + Settings.Minute * 10;
                ProcessAuction();
            }

            if (Time >= spawnTime)
            {
                spawnTime = Time + Settings.Second * 10;
                Main.RespawnTick.Process();
            }

            if (Time >= robotTime)
            {
                robotTime = Time + Settings.Minute;
                Robot.Process(RobotNPC);
            }

            if (Time >= timerTime)
            {
                timerTime = Time + Settings.Second;

                string[] keys = Timers.Keys.ToArray();

                foreach (var key in keys)
                {
                    if (Timers[key].RelativeTime <= Time)
                    {
                        Timers.Remove(key);
                    }
                }
            }
        }

        private void ProcessAuction()
        {
            LinkedListNode<AuctionInfo> current = Auctions.First;

            while (current != null)
            {
                AuctionInfo info = current.Value;

                if (!info.Expired && !info.Sold && Now >= info.ConsignmentDate.AddDays(Globals.ConsignmentLength))
                {
                    if (info.ItemType == MarketItemType.Auction && info.CurrentBid > info.Price)
                    {
                        string message = LanguageMgr.Get("YouWonForGold", info.Item.FriendlyName, info.CurrentBid);

                        info.Sold = true;
                        MailCharacter(info.CurrentBuyerInfo, item: info.Item, customMessage: message);

                        MessageAccount(info.CurrentBuyerInfo.AccountInfo, LanguageMgr.Get("YouBoughtForGold", info.Item.FriendlyName, info.CurrentBid),
                            ChatType.System);
                        MessageAccount(info.SellerInfo.AccountInfo, LanguageMgr.Get("YouSoldForGold", info.Item.FriendlyName, info.CurrentBid),
                            ChatType.System);
                    }
                    else
                    {
                        info.Expired = true;
                    }
                }

                current = current.Next;
            }
        }

        public void Broadcast(Packet p)
        {
            for (var i = 0; i < Players.Count; i++) Players[i].Enqueue(p);
        }

        public void RequiresBaseStatUpdate()
        {
            for (var i = 0; i < Players.Count; i++) Players[i].HasUpdatedBaseStats = false;
        }

        public void RequiresHeroBaseStatUpdate()
        {
            for (var i = 0; i < Heroes.Count; i++)
            {
                Heroes[i].HasUpdatedBaseStats = false;
                Heroes[i].RefreshStats();
            }
        }



    }
}
