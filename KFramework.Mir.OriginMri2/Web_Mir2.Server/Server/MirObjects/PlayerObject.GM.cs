// partial 文件：PlayerObject 功能拆分（原 PlayerObject.cs 原样迁入）。
using System.Drawing;
using C = ClientPackets;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using S = ServerPackets;
using System.Text.RegularExpressions;
using Timer = Server.MirEnvir.Timer;

namespace Server.MirObjects
{
    public partial class PlayerObject
    {
        public void GMCommand(string message, List<ChatItem> linkedItems = null)
        {
            message = message.Replace("$pos", Functions.PointToString(CurrentLocation));
            Packet p;
            string[] parts = null;

            if (message.StartsWith("@!"))
            {
                if (!IsGM) return;

                message = String.Format("(*){0}:{1}", Name, message.Remove(0, 2));

                message = ProcessChatItems(message, Envir.Players, linkedItems);

                p = new S.Chat { Message = message, Type = ChatType.System };

                Envir.Broadcast(p);
            }
            else
            {

                //Command
                message = message.Remove(0, 1);
                parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) return;

                PlayerObject player;
                CharacterInfo data;
                String hintstring;
                UserItem item;

                List<int> conquestAIs = new()
                {
                    72, // siege gate
                    73, // gate west
                    80, // archer
                    81, // gate 
                    82  // wall
                };

                switch (parts[0].ToUpper())
                {
                    case "LOGIN":
                        GMLogin = true;
                        ReceiveChat(LanguageMgr.Get("EnterGmPassword"), ChatType.System);
                        return;

                    case "KILL":
                        if (!IsGM) return;

                        if (parts.Length >= 2)
                        {
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(LanguageMgr.Get("CouldNotFind", parts[1]), ChatType.System);
                                return;
                            }

                            if (!player.GMNeverDie)
                            {
                                player.Die();

                                Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("KilledByGM", player.Name, Name));
                                Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("KilledByGM", player.Name, Name));
                            }
                        }
                        else
                        {
                            if (!CurrentMap.ValidPoint(Front)) return;

                            Cell cell = CurrentMap.GetCell(Front);

                            if (cell == null || cell.Objects == null) return;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];

                                switch (ob.Race)
                                {
                                    case ObjectType.Player:
                                    case ObjectType.Monster:
                                        if (ob.Dead) continue;
                                        ob.EXPOwner = this;
                                        ob.ExpireTime = Envir.Time + MonsterObject.EXPOwnerDelay;
                                        ob.Die();

                                        if (ob is PlayerObject killedPlayer)
                                        {
                                            Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("KilledByGM", killedPlayer.Name, Name));
                                        }
                                        break;

                                    default:
                                        continue;
                                }
                            }
                        }
                        return;

                    case "CHANGEGENDER":
                        if (!IsGM && !Settings.TestServer) return;

                        data = parts.Length < 2 ? Info : Envir.GetCharacterInfo(parts[1]);

                        if (data == null) return;

                        switch (data.Gender)
                        {
                            case MirGender.Male:
                                data.Gender = MirGender.Female;
                                break;
                            case MirGender.Female:
                                data.Gender = MirGender.Male;
                                break;
                        }

                        ReceiveChat(LanguageMgr.Get("PlayerChangedTo", data.Name, data.Gender), ChatType.System);
                        MessageQueue.Enqueue(string.Format("Player {0} has been changed to {1} by {2}", data.Name, data.Gender, Name));

                        Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("PlayerGenderChangedByGM", data.Player.Name, data.Gender.ToString(), Name));

                        if (data.Player != null)
                            data.Player.Connection.LogOut();

                        break;

                    case "LEVEL":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        ushort level;
                        ushort old;
                        if (parts.Length >= 3)
                        {
                            if (!IsGM) return;

                            if (ushort.TryParse(parts[2], out level))
                            {
                                if (level == 0) return;
                                player = Envir.GetPlayer(parts[1]);
                                if (player == null) return;
                                old = player.Level;
                                player.Level = level;
                                player.LevelUp();

                                ReceiveChat(LanguageMgr.Get("PlayerHasBeenLevelled", player.Name, old, player.Level), ChatType.System);
                                MessageQueue.Enqueue(string.Format("Player {0} has been Leveled {1} -> {2} by {3}", player.Name, old, player.Level, Name));
                                Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("PlayerLevelledByGM", player.Name, old, player.Level, Name));

                                return;
                            }
                        }
                        else
                        {
                            if (parts[1] == "-1")
                            {
                                parts[1] = ushort.MaxValue.ToString();
                            }

                            if (ushort.TryParse(parts[1], out level))
                            {
                                if (level == 0) return;
                                old = Level;
                                Level = level;
                                LevelUp();

                                ReceiveChat(string.Format("{0} {1} -> {2}.", LanguageMgr.Get("LevelUp"), old, Level), ChatType.System);
                                MessageQueue.Enqueue(string.Format("Player {0} has been Leveled {1} -> {2} by {3}", Name, old, Level, Name));
                                return;
                            }
                        }

                        ReceiveChat(LanguageMgr.Get("CouldNotLevelPlayer"), ChatType.System);
                        break;

                    case "LEVELHERO":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        if (parts.Length >= 3)
                        {
                            if (!IsGM) return;

                            if (ushort.TryParse(parts[2], out level))
                            {
                                if (level == 0) return;
                                player = Envir.GetPlayer(parts[1]);
                                if (player == null) return;
                                HeroObject hero = player.GetHero();
                                if (hero == null) return;
                                old = hero.Level;
                                hero.Level = level;
                                hero.LevelUp();

                                ReceiveChat(LanguageMgr.Get("PlayerHeroLevelled", player.Name, old, hero.Level), ChatType.System);
                                MessageQueue.Enqueue(string.Format("Player {0}'s hero has been Levelled {1} -> {2} by {3}", player.Name, old, hero.Level, Name));
                                Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("PlayerHeroLevelledByGM", player.Name, old, hero.Level, Name));
                                return;
                            }
                        }
                        else
                        {
                            HeroObject hero = GetHero();
                            if (hero == null) return;

                            if (parts[1] == "-1")
                            {
                                parts[1] = ushort.MaxValue.ToString();
                            }

                            if (ushort.TryParse(parts[1], out level))
                            {
                                if (level == 0) return;
                                old = hero.Level;
                                hero.Level = level;
                                hero.LevelUp();

                                ReceiveChat(string.Format("{0} {1} -> {2}.", LanguageMgr.Get("LevelUp"), old, hero.Level), ChatType.System);
                                MessageQueue.Enqueue(string.Format("Player {0}'s hero has been Leveled {1} -> {2} by {3}", Name, old, hero.Level, Name));
                                return;
                            }
                        }

                        ReceiveChat(LanguageMgr.Get("CouldNotLevelPlayer"), ChatType.System);
                        break;

                    case "MAKE":
                        {
                            if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                            ItemInfoTable iInfo;
                            int itemIndex = 0;

                            if (Int32.TryParse(parts[1], out itemIndex))
                            {
                                iInfo = Envir.GetItemInfo(itemIndex);
                            }
                            else
                            {
                                iInfo = Envir.GetItemInfo(parts[1]);
                            }

                            if (iInfo == null) return;

                            ushort itemCount = 1;
                            if (parts.Length >= 3 && !ushort.TryParse(parts[2], out itemCount))
                                itemCount = 1;

                            var tempCount = itemCount;

                            while (itemCount > 0)
                            {
                                if (iInfo.StackSize >= itemCount)
                                {
                                    item = Envir.CreateDropItem(iInfo);
                                    item.GMMade = true;
                                    item.Count = itemCount;

                                    if (CanGainItem(item)) GainItem(item);

                                    return;
                                }
                                item = Envir.CreateDropItem(iInfo);
                                item.GMMade = true;
                                item.Count = (ushort)Math.Min(iInfo.StackSize, ushort.MaxValue);
                                itemCount -= (ushort)iInfo.StackSize;

                                if (!CanGainItem(item)) return;
                                GainItem(item);
                            }

                            ReceiveChat(LanguageMgr.Get("ItemHasBeenCreated", iInfo.FriendlyName, tempCount), ChatType.System);
                            MessageQueue.Enqueue(string.Format("Player {0} has attempted to Create {1} x{2}", Name, iInfo.FriendlyName, tempCount));
                        }
                        break;
                    case "CLEARBUFFS":
                        foreach (var buff in Buffs)
                        {
                            buff.FlagForRemoval = true;
                            buff.ExpireTime = 0;
                        }
                        break;

                    case "CLEARBAG":
                        if (!IsGM && !Settings.TestServer) return;
                        player = this;

                        if (parts.Length >= 2)
                            player = Envir.GetPlayer(parts[1]);

                        if (player == null) return;
                        for (int i = 0; i < player.Info.Inventory.Length; i++)
                        {
                            item = player.Info.Inventory[i];
                            if (item == null) continue;

                            player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                            player.Info.Inventory[i] = null;
                        }
                        player.RefreshStats();
                        break;

                    case "SUPERMAN":
                        if (!IsGM && !Settings.TestServer) return;

                        GMNeverDie = !GMNeverDie;

                        hintstring = GMNeverDie ? LanguageMgr.Get("InvincibleMode"): LanguageMgr.Get("NormalMode");
                        ReceiveChat(hintstring, ChatType.System);
                        UpdateGMBuff();
                        break;

                    case "GAMEMASTER":
                        if (!IsGM && !Settings.TestServer) return;

                        GMGameMaster = !GMGameMaster;

                        hintstring = GMGameMaster ? LanguageMgr.Get("GameMasterMode"): LanguageMgr.Get("NormalMode");
                        ReceiveChat(hintstring, ChatType.System);
                        UpdateGMBuff();
                        break;

                    case "OBSERVER":
                        if (!IsGM) return;
                        Observer = !Observer;

                        hintstring = Observer ? LanguageMgr.Get("ObserverMode"): LanguageMgr.Get("NormalMode");
                        ReceiveChat(hintstring, ChatType.System);
                        UpdateGMBuff();
                        break;
                    case "ALLOWGUILD":
                        EnableGuildInvite = !EnableGuildInvite;
                        hintstring = EnableGuildInvite ? LanguageMgr.Get("GuildInvitesEnabled"): LanguageMgr.Get("GuildInvitesDisabled");
                        ReceiveChat(hintstring, ChatType.System);
                        break;
                    case "RECALL":
                        if (!IsGM) return;

                        if (parts.Length < 2) return;
                        player = Envir.GetPlayer(parts[1]);

                        if (player == null) return;

                        player.Teleport(CurrentMap, Front);
                        break;
                    case "OBSERVE":
                        if (parts.Length < 2) return;
                        player = Envir.GetPlayer(parts[1]);

                        if (player == null) return;
                        if ((!player.AllowObserve || !Settings.AllowObserve) && !IsGM) return;

                        player.AddObserver(Connection);
                        break;
                    case "ENABLEGROUPRECALL":
                        EnableGroupRecall = !EnableGroupRecall;
                        hintstring = EnableGroupRecall ? LanguageMgr.Get("GroupRecallEnabled"): LanguageMgr.Get("GroupRecallDisabled");
                        ReceiveChat(hintstring, ChatType.System);
                        break;

                    case "GROUPRECALL":
                        if (GroupMembers == null || GroupMembers[0] != this || Dead)
                            return;

                        if (CurrentMap.Info.NoRecall)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallOnMap"), ChatType.System);
                            return;
                        }

                        if (Envir.Time < LastRecallTime)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallForSeconds", (LastRecallTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }

                        if (ItemSets.Any(set => set.Set == ItemSet.Recall && set.SetComplete))
                        {
                            LastRecallTime = Envir.Time + 180000;
                            for (var i = 1; i < GroupMembers.Count(); i++)
                            {
                                if (GroupMembers[i].EnableGroupRecall)
                                    GroupMembers[i].Teleport(CurrentMap, CurrentLocation);
                                else
                                    GroupMembers[i].ReceiveChat(LanguageMgr.Get("RecallAttemptWithoutPermission"),
                                        ChatType.System);
                            }
                        }
                        break;
                    case "RECALLMEMBER":
                        if (GroupMembers == null || GroupMembers[0] != this)
                        {
                            ReceiveChat(LanguageMgr.Get("NotGroupLeader"), ChatType.System);
                            return;
                        }

                        if (Dead)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallWhenDead"), ChatType.System);
                            return;
                        }

                        if (CurrentMap.Info.NoRecall)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallOnMap"), ChatType.System);
                            return;
                        }

                        if (Envir.Time < LastRecallTime)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallSeconds", (LastRecallTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }
                        if (ItemSets.Any(set => set.Set == ItemSet.Recall && set.SetComplete))
                        {
                            if (parts.Length < 2) return;
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null || !IsMember(player) || this == player)
                            {
                                ReceiveChat((LanguageMgr.Get("PlayerCouldNotFound", parts[1])), ChatType.System);
                                return;
                            }
                            if (!player.EnableGroupRecall)
                            {
                                player.ReceiveChat(LanguageMgr.Get("RecallAttemptWithoutPermission"), ChatType.System);
                                ReceiveChat((LanguageMgr.Get("BlockingGroupRecall", player.Name)), ChatType.System);
                                return;
                            }
                            LastRecallTime = Envir.Time + 60000;

                            if (!player.Teleport(CurrentMap, Front))
                                player.Teleport(CurrentMap, CurrentLocation);
                        }
                        else
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallWithoutRecallset"), ChatType.System);
                            return;
                        }
                        break;

                    case "RECALLLOVER":
                        if (Info.Married == 0)
                        {
                            ReceiveChat(LanguageMgr.Get("YouAreNotMarried"), ChatType.System);
                            return;
                        }

                        if (Dead)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallWhenDead"), ChatType.System);
                            return;
                        }

                        if (CurrentMap.Info.NoRecall)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallOnMap"), ChatType.System);
                            return;
                        }

                        if (Info.Equipment[(int)EquipmentSlot.RingL] == null)
                        {
                            ReceiveChat(LanguageMgr.Get("NeedWeddingRingForRecall"), ChatType.System);
                            return;
                        }


                        if (Info.Equipment[(int)EquipmentSlot.RingL].WeddingRing == Info.Married)
                        {
                            CharacterInfo Lover = Envir.GetCharacterInfo(Info.Married);

                            if (Lover == null) return;

                            player = Envir.GetPlayer(Lover.Name);

                            if (!Settings.WeddingRingRecall)
                            {
                                ReceiveChat(LanguageMgr.Get("TeleportationViaWeddingRingDisabled"), ChatType.System);
                                return;
                            }

                            if (player == null)
                            {
                                ReceiveChat((LanguageMgr.Get("PlayerIsNotOnline", Lover.Name)), ChatType.System);
                                return;
                            }

                            if (player.Dead)
                            {
                                ReceiveChat(LanguageMgr.Get("CannotRecallDeadPlayer"), ChatType.System);
                                return;
                            }

                            if (player.Info.Equipment[(int)EquipmentSlot.RingL] == null)
                            {
                                player.ReceiveChat((LanguageMgr.Get("NeedWeddingRingRecall", Lover.Name)), ChatType.System);
                                ReceiveChat((LanguageMgr.Get("NotWearingWeddingRing", Lover.Name)), ChatType.System);
                                return;
                            }

                            if (player.Info.Equipment[(int)EquipmentSlot.RingL].WeddingRing != player.Info.Married)
                            {
                                player.ReceiveChat((LanguageMgr.Get("NeedWeddingRingForRecallOnLeft", Lover.Name)), ChatType.System);
                                ReceiveChat((LanguageMgr.Get("NotWearingWeddingRing", Lover.Name)), ChatType.System);
                                return;
                            }

                            if (!player.AllowLoverRecall)
                            {
                                player.ReceiveChat(LanguageMgr.Get("RecallWithoutPermission"), ChatType.System);
                                ReceiveChat((LanguageMgr.Get("BlockingLoverRecall", player.Name)), ChatType.System);
                                return;
                            }

                            if ((Envir.Time < LastRecallTime) && (Envir.Time < player.LastRecallTime))
                            {
                                ReceiveChat(LanguageMgr.Get("CannotRecallSeconds", (LastRecallTime - Envir.Time) / 1000), ChatType.System);
                                return;
                            }

                            LastRecallTime = Envir.Time + 60000;
                            player.LastRecallTime = Envir.Time + 60000;

                            if (!player.Teleport(CurrentMap, Front))
                                player.Teleport(CurrentMap, CurrentLocation);
                        }
                        else
                        {
                            ReceiveChat(LanguageMgr.Get("CannotRecallLoverWithoutRing"), ChatType.System);
                            return;
                        }
                        break;
                    case "TIME":
                        ReceiveChat(LanguageMgr.Get("TheTimeIs", Envir.Now.ToString("hh:mm tt")), ChatType.System);
                        break;

                    case "ROLL":
                        int diceNum = Envir.Random.Next(5) + 1;

                        if (GroupMembers == null) { return; }

                        for (int i = 0; i < GroupMembers.Count; i++)
                        {
                            PlayerObject playerSend = GroupMembers[i];
                            playerSend.ReceiveChat(LanguageMgr.Get("HasRolledNumber", Name, diceNum), ChatType.Team);
                        }
                        break;

                    case "COSTUME":
                        {
                            if (!IsGM) return;

                            if (parts.Length < 3)
                            {
                                ReceiveChat("用法: @COSTUME <zhuangban|huanwu|foot|title|huiji|zhenfa|xuanguang|douli|miansha|yupei|xunzhang|hudun|hujian|pifeng|liaoshang|all> <id|0>", ChatType.System);
                                return;
                            }

                            string cat = parts[1].ToLower();
                            if (!int.TryParse(parts[2], out int id))
                            {
                                ReceiveChat("id 必须是数字 (0 = 清除当前层)", ChatType.System);
                                return;
                            }

                            switch (cat)
                            {
                                case "zhuangban": Info.ActiveZhuangBan = id; break;
                                case "huanwu":    Info.ActiveHuanWu = id; break;
                                case "foot":      Info.ActiveFoot = id; break;
                                case "title":     Info.ActiveTitle = id; break;
                                case "huiji":     Info.ActiveHuiJi = id; break;
                                case "zhenfa":    Info.ActiveZhenFa = id; break;
                                case "xuanguang": Info.ActiveXuanGuang = id; break;
                                case "douli":     Info.ActiveDouLi = id; break;
                                case "miansha":   Info.ActiveMianSha = id; break;
                                case "yupei":     Info.ActiveYuPei = id; break;
                                case "xunzhang":  Info.ActiveXunZhang = id; break;
                                case "hudun":     Info.ActiveHuDun = id; break;
                                case "hujian":    Info.ActiveHuJian = id; break;
                                case "pifeng":    Info.ActivePiFeng = id; break;
                                case "liaoshang": Info.ActiveLiaoShang = id; break;
                                case "all":
                                    Info.ActiveZhuangBan = Info.ActiveHuanWu = Info.ActiveFoot = Info.ActiveTitle =
                                        Info.ActiveHuiJi = Info.ActiveZhenFa = Info.ActiveXuanGuang =
                                        Info.ActiveDouLi = Info.ActiveMianSha = Info.ActiveYuPei = Info.ActiveXunZhang =
                                        Info.ActiveHuDun = Info.ActiveHuJian = Info.ActivePiFeng = Info.ActiveLiaoShang = id;
                                    break;
                                default:
                                    ReceiveChat("未知分类，可用: zhuangban/huanwu/foot/title/huiji/zhenfa/xuanguang/douli/miansha/yupei/xunzhang/hudun/hujian/pifeng/liaoshang/all", ChatType.System);
                                    return;
                            }

                            // 立即重绘：广播新的 ObjectPlayer 包给视野内玩家（含自己），客户端收到后重新 Load 即点亮对应层。
                            BroadcastInfo();
                            ReceiveChat(string.Format("已设置装扮层 [{0}] = {1}（已重绘；若本机未即时更新，移动一下或重登即可）", cat, id), ChatType.System);
                        }
                        break;

                    case "MAP":
                        var mapName = CurrentMap.Info.FileName;
                        var mapTitle = CurrentMap.Info.Title;
                        ReceiveChat((LanguageMgr.Get("YouAreInMapId", mapTitle, mapName)), ChatType.System);
                        break;

                    case "BACKUPPLAYER":
                        {
                            if (!IsGM || parts.Length < 2) return;

                            var info = Envir.GetCharacterInfo(parts[1]);

                            if (info == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }

                            Envir.SaveArchivedCharacter(info);

                            ReceiveChat(LanguageMgr.Get("PlayerHasBeenBackedUp", info.Name), ChatType.System);
                            MessageQueue.Enqueue(string.Format("Player {0} has been backed up by {1}", info.Name, Name));
                        }
                        break;

                    case "ARCHIVEPLAYER":
                        {
                            if (!IsGM || parts.Length < 2) return;

                            data = Envir.GetCharacterInfo(parts[1]);

                            if (data == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }

                            if (data == Info)
                            {
                                ReceiveChat(LanguageMgr.Get("CannotArchiveCurrentPlayer"), ChatType.System);
                                return;
                            }

                            var account = Envir.GetAccountByCharacter(parts[1]);

                            if (account == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFoundInAccount", parts[1]), ChatType.System);
                                return;
                            }

                            Envir.SaveArchivedCharacter(data);

                            Envir.CharacterList.Remove(data);
                            account.Characters.Remove(data);

                            ReceiveChat(LanguageMgr.Get("PlayerHasBeenArchived", data.Name), ChatType.System);
                            MessageQueue.Enqueue(string.Format("Player {0} has been archived by {1}", data.Name, Name));
                        }
                        break;

                    case "LOADPLAYER":
                        {
                            if (!IsGM) return;

                            if (parts.Length < 2) return;

                            var bak = Envir.GetArchivedCharacter(parts[1]);

                            if (bak == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerCouldNotBeLoaded", parts[1]), ChatType.System);
                                return;
                            }

                            var info = Envir.GetCharacterInfo(bak.Name);

                            if (info == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }

                            if (info.Index != bak.Index)
                            {
                                ReceiveChat(LanguageMgr.Get("CannotLoadPlayerIdMismatch"), ChatType.System);
                                return;
                            }

                            info = bak;

                            ReceiveChat(LanguageMgr.Get("PlayerHasBeenLoaded", info.Name), ChatType.System);
                            MessageQueue.Enqueue(string.Format("Player {0} has been loaded by {1}", info.Name, Name));
                        }
                        break;

                    case "RESTOREPLAYER":
                        {
                            if (!IsGM || parts.Length < 2) return;

                            AccountInfo account = null;

                            if (parts.Length > 2)
                            {
                                if (!Envir.AccountExists(parts[2]))
                                {
                                    ReceiveChat(LanguageMgr.Get("AccountNotFound", parts[2]), ChatType.System);
                                    return;
                                }

                                account = Envir.GetAccount(parts[2]);

                                if (account.Characters.Count >= Globals.MaxCharacterCount)
                                {
                                    ReceiveChat(LanguageMgr.Get("AccountHasCharacters", parts[2], Globals.MaxCharacterCount), ChatType.System);
                                    return;
                                }
                            }

                            data = Envir.GetCharacterInfo(parts[1]);

                            if (data == null)
                            {
                                if (account != null)
                                {
                                    data = Envir.GetArchivedCharacter(parts[1]);

                                    if (data == null)
                                    {
                                        ReceiveChat(LanguageMgr.Get("PlayerRestoreFailFullFilename", parts[1]), ChatType.System);
                                        return;
                                    }

                                    data.AccountInfo = account;

                                    account.Characters.Add(data);
                                    Envir.CharacterList.Add(data);

                                    data.Deleted = false;
                                    data.DeleteDate = DateTime.MinValue;

                                    data.LastLoginDate = Envir.Now;
                                }
                                else
                                {
                                    ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                    return;
                                }
                            }
                            else
                            {
                                if (!data.Deleted) return;
                                data.Deleted = false;
                                data.DeleteDate = DateTime.MinValue;
                            }

                            ReceiveChat(LanguageMgr.Get("PlayerRestoredBy", data.Name, Name), ChatType.System);
                            MessageQueue.Enqueue(string.Format("Player {0} has been restored by {1}", data.Name, Name));
                        }
                        break;

                    case "MOVE":
                        if (!IsGM && !SpecialMode.HasFlag(SpecialItemMode.Teleport) && !Settings.TestServer) return;
                        if (!IsGM && CurrentMap.Info.NoPosition)
                        {
                            ReceiveChat((LanguageMgr.Get("CannotPositionMoveOnMap")), ChatType.System);
                            return;
                        }
                        if (Envir.Time < LastTeleportTime)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotTeleportSecondsLeft", (LastTeleportTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }

                        int x, y;

                        if (parts.Length <= 2 || !int.TryParse(parts[1], out x) || !int.TryParse(parts[2], out y))
                        {
                            if (!IsGM)
                                LastTeleportTime = Envir.Time + 10000;
                            TeleportRandom(200, 0);
                            return;
                        }
                        if (!IsGM)
                            LastTeleportTime = Envir.Time + 10000;
                        Teleport(CurrentMap, new Point(x, y));
                        break;

                    case "MAPMOVE":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;
                        var instanceID = 1; x = 0; y = 0;

                        if (parts.Length == 3 || parts.Length == 5)
                            int.TryParse(parts[2], out instanceID);

                        if (instanceID < 1) instanceID = 1;

                        var map = Envir.GetMapByNameAndInstance(parts[1], instanceID);
                        if (map == null)
                        {
                            ReceiveChat((LanguageMgr.Get("MapCouldNotBeFound", parts[1], instanceID)), ChatType.System);
                            return;
                        }

                        if (parts.Length == 4 || parts.Length == 5)
                        {
                            int.TryParse(parts[parts.Length - 2], out x);
                            int.TryParse(parts[parts.Length - 1], out y);
                        }

                        switch (parts.Length)
                        {
                            case 2:
                                ReceiveChat(TeleportRandom(200, 0, map) ? (LanguageMgr.Get("MovedToMap1", map.Info.FileName)) :
                                    (LanguageMgr.Get("FailedMovementToMap1", map.Info.FileName)), ChatType.System);
                                break;
                            case 3:
                                ReceiveChat(TeleportRandom(200, 0, map) ? (LanguageMgr.Get("MovedToMap2", map.Info.FileName, instanceID)) :
                                    (LanguageMgr.Get("FailedMovementToMap2", map.Info.FileName, instanceID)), ChatType.System);
                                break;
                            case 4:
                                ReceiveChat(Teleport(map, new Point(x, y)) ? (LanguageMgr.Get("MovedToMap3", map.Info.FileName, x, y)) :
                                    (LanguageMgr.Get("FailedMovementToMap3", map.Info.FileName, x, y)), ChatType.System);
                                break;
                            case 5:
                                ReceiveChat(Teleport(map, new Point(x, y)) ? (LanguageMgr.Get("MovedToMap4", map.Info.FileName, instanceID, x, y)) :
                                    (LanguageMgr.Get("FailedMovementToMap4", map.Info.FileName, instanceID, x, y)), ChatType.System);
                                break;
                        }
                        break;

                    case "GOTO":
                        if (!IsGM) return;

                        if (parts.Length < 2) return;
                        player = Envir.GetPlayer(parts[1]);

                        if (player == null) return;

                        Teleport(player.CurrentMap, player.CurrentLocation);
                        break;

                    case "MOB":
                        if (!IsGM && !Settings.TestServer) return;
                        if (parts.Length < 2)
                        {
                            ReceiveChat(LanguageMgr.Get("NotEnoughParamsSpawnMonster"), ChatType.System);
                            return;
                        }

                        MonsterInfoTable mInfo = null;
                        int monsterIndex = 0;

                        if (Int32.TryParse(parts[1], out monsterIndex))
                        {
                            mInfo = Envir.GetMonsterInfo(monsterIndex, false);
                        }
                        else
                        {
                            mInfo = Envir.GetMonsterInfo(parts[1]);
                        }

                        if (mInfo == null)
                        {
                            ReceiveChat((LanguageMgr.Get("MonsterDoesNotExist", parts[1])), ChatType.System);
                            return;
                        }

                        if (conquestAIs.Contains(mInfo.AI))
                        {
                            ReceiveChat(LanguageMgr.Get("CannotSpawnConquestItem", mInfo.Name), ChatType.System);
                            return;
                        }

                        uint count = 1;
                        if (parts.Length >= 3 && IsGM)
                            if (!uint.TryParse(parts[2], out count)) count = 1;
                        int spread = 0;
                        if (parts.Length >= 4)
                            int.TryParse(parts[3], out spread);

                        for (int i = 0; i < count; i++)
                        {
                            MonsterObject monster = MonsterObject.GetMonster(mInfo);
                            if (monster == null)
                            {
                                return;
                            }

                            if (monster is IntelligentCreatureObject)
                            {
                                ReceiveChat(LanguageMgr.Get("CannotSpawnIntelligentCreature"), ChatType.System);
                                return;
                            }

                            monster.GMMade = true;

                            if (spread == 0)
                                monster.Spawn(CurrentMap, Front);
                            else
                                for (int _ = 0; _ < 20; _++)
                                    if (monster.Spawn(CurrentMap, CurrentLocation.Add(Envir.Random.Next(-spread, spread + 1), Envir.Random.Next(-spread, spread + 1))))
                                        break;
                        }

                        ReceiveChat((LanguageMgr.Get("MonsterSpawned", mInfo.Name, count)), ChatType.System);
                        break;

                    case "RECALLMOB":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        MonsterInfoTable mInfo2 = null;
                        int monsterIndex2 = 0;

                        if (Int32.TryParse(parts[1], out monsterIndex2))
                        {
                            mInfo2 = Envir.GetMonsterInfo(monsterIndex2, false);
                        }
                        else
                        {
                            mInfo2 = Envir.GetMonsterInfo(parts[1]);
                        }

                        if (mInfo2 == null) return;

                        count = 1;
                        byte petlevel = 0;

                        if (parts.Length > 2)
                            if (!uint.TryParse(parts[2], out count) || count > 50) count = 1;

                        if (parts.Length > 3)
                            if (!byte.TryParse(parts[3], out petlevel) || petlevel > 7) petlevel = 0;

                        if (!IsGM && (Pets.Count(t => !t.Dead && t.Race != ObjectType.Creature) >= Globals.MaxPets)) return;

                        for (int i = 0; i < count; i++)
                        {
                            MonsterObject monster = MonsterObject.GetMonster(mInfo2);

                            if (monster == null) return;

                            if (conquestAIs.Contains(monster.Info.AI))
                            {
                                ReceiveChat(LanguageMgr.Get("CannotSpawnConquestItem", monster.Name), ChatType.System);
                                return;
                            }
                            else if (monster is IntelligentCreatureObject)
                            {
                                ReceiveChat(LanguageMgr.Get("CannotSpawnIntelligentCreature"), ChatType.System);
                                return;
                            }

                            monster.PetLevel = petlevel;
                            monster.Master = this;
                            monster.MaxPetLevel = 7;
                            monster.Direction = Direction;
                            monster.ActionTime = Envir.Time + 1000;
                            monster.Spawn(CurrentMap, Front);
                            Pets.Add(monster);
                        }

                        ReceiveChat((LanguageMgr.Get("PetHasBeenRecalled", mInfo2.Name, count)), ChatType.System);
                        break;

                    case "RELOADDROPS":
                        if (!IsGM) return;

                        Envir.ReloadDrops();

                        ReceiveChat(LanguageMgr.Get("DropsReloaded"), ChatType.System);
                        break;

                    case "RELOADNPCS":
                        if (!IsGM) return;

                        Envir.ReloadNPCs();

                        ReceiveChat(LanguageMgr.Get("NpcScriptsReloaded"), ChatType.System);
                        break;

                    case "CLEARIPBLOCKS":
                        if (!IsGM) return;

                        Envir.IPBlocks.Clear();
                        break;

                    case "GIVEGOLD":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        player = this;

                        if (parts.Length > 2)
                        {
                            if (!IsGM) return;

                            if (!uint.TryParse(parts[2], out count)) return;
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }
                        }

                        else if (!uint.TryParse(parts[1], out count)) return;

                        if (count + player.Info.Gold >= uint.MaxValue)
                            count = uint.MaxValue - player.Info.Gold;

                        player.GainGold(count);

                        string goldMsg = LanguageMgr.Get("PlayerGivenGoldByGM", player.Name, count, Name);
                        MessageQueue.Enqueue(goldMsg);
                        Helpers.ChatSystem.SystemMessage(chatMessage: goldMsg);

                        break;

                    case "GIVEPEARLS":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        player = this;

                        if (parts.Length > 2)
                        {
                            if (!IsGM) return;

                            if (!uint.TryParse(parts[2], out count)) return;
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }
                        }

                        else if (!uint.TryParse(parts[1], out count)) return;

                        if (count + player.Info.PearlCount >= int.MaxValue)
                            count = (uint)(int.MaxValue - player.Info.PearlCount);

                        player.IntelligentCreatureGainPearls((int)count);

                        string pearlMsg = LanguageMgr.Get("PlayerGivenPearlByGM", player.Name, count, Name);

                        MessageQueue.Enqueue(pearlMsg);
                        Helpers.ChatSystem.SystemMessage(chatMessage: pearlMsg);

                        break;
                    case "GIVECREDIT":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        player = this;

                        if (parts.Length > 2)
                        {
                            if (!IsGM) return;

                            if (!uint.TryParse(parts[2], out count)) return;
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }
                        }

                        else if (!uint.TryParse(parts[1], out count)) return;

                        if (count + player.Info.Credit >= uint.MaxValue)
                            count = uint.MaxValue - player.Info.Credit;

                        player.GainCredit(count);

                        string creditMsg = LanguageMgr.Get("PlayerGivenCreditByGM", player.Name, count, Name);

                        MessageQueue.Enqueue(string.Format("Player {0} has been given {1} credit", player.Name, count));
                        Helpers.ChatSystem.SystemMessage(chatMessage: creditMsg);

                        break;
                    case "GIVESKILL":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 3) return;

                        byte spellLevel = 0;

                        player = this;
                        Spell skill;

                        // 允许原始传奇技能 id（如 1000097，超出 Spell 枚举定义但 Enum.TryParse 数字串可解析）。
                        if (!Enum.TryParse(parts.Length > 3 ? parts[2] : parts[1], true, out skill)) return;

                        if (skill == Spell.无) return;

                        spellLevel = byte.TryParse(parts.Length > 3 ? parts[3] : parts[2], out spellLevel) ? Math.Min((byte)3, spellLevel) : (byte)0;

                        if (parts.Length > 3)
                        {
                            if (!IsGM) return;

                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }
                        }

                        var magic = new MagicInfoRunItem(skill) { MaxLevel = spellLevel };

                        if (magic.Info == null)  // 技能表里不存在该 Spell（非法 id）
                        {
                            ReceiveChat($"技能不存在: {parts[2]}", ChatType.System);
                            return;
                        }

                        if (player.Info.Magics.Any(e => e.Spell == skill))
                        {
                            var existing = player.Info.Magics.FirstOrDefault(e => e.Spell == skill);
                            existing.MaxLevel = spellLevel;
                            existing.Level = 0;
                            existing.Experience = 0;

                            player.SendMagicInfo(existing);

                            string skillChangeMsg = LanguageMgr.Get("SpellChangedByGM", player.Name, skill.ToString(), spellLevel, Name);

                            player.ReceiveChat(LanguageMgr.Get("SpellChangedLevel", skill.ToString(), spellLevel), ChatType.System);
                            Helpers.ChatSystem.SystemMessage(chatMessage: skillChangeMsg);

                            return;
                        }
                        else
                        {
                            player.ReceiveChat(LanguageMgr.Get("LearnedSkillAtLevel", skill.ToString(), spellLevel), ChatType.System);

                            if (player != this)
                            {
                                ReceiveChat(LanguageMgr.Get("HasLearnedSkillAtLevel", player.Name, skill.ToString(), spellLevel), ChatType.System);
                            }

                            string skillLearnedMg = LanguageMgr.Get("SpellLearntSetLevelByGM", player.Name, skill.ToString(), spellLevel, Name);
                            Helpers.ChatSystem.SystemMessage(chatMessage: skillLearnedMg);

                            player.Info.Magics.Add(magic);
                        }

                        player.SendMagicInfo(magic);
                        player.RefreshStats();
                        break;

                    case "FIND":
                        if (!IsGM && !SpecialMode.HasFlag(SpecialItemMode.Probe)) return;

                        if (Envir.Time < LastProbeTime)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotSearchSeconds", (LastProbeTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }

                        if (parts.Length < 2) return;
                        player = Envir.GetPlayer(parts[1]);

                        if (player == null)
                        {
                            ReceiveChat(LanguageMgr.Get("PlayerIsNotOnline", parts[1]), ChatType.System);
                            return;
                        }
                        if (player.CurrentMap == null) return;
                        if (!IsGM)
                            LastProbeTime = Envir.Time + 180000;
                        ReceiveChat((LanguageMgr.Get("PlayerLocationInfo", player.Name, player.CurrentMap.Info.Title, player.CurrentLocation.X, player.CurrentLocation.Y)), ChatType.System);
                        break;

                    case "LEAVEGUILD":
                        if (MyGuild == null) return;
                        if (MyGuildRank == null) return;
                        if (MyGuild.IsAtWar())
                        {
                            ReceiveChat(LanguageMgr.Get("CannotLeaveGuildAtWar"), ChatType.System);
                            return;
                        }
                        if (MyGuild.Name == Settings.NewbieGuild && Settings.NewbieGuildBuffEnabled == true) RemoveBuff(BuffType.Newbie);
                        if (HasBuff(BuffType.Guild)) RemoveBuff(BuffType.Guild);
                        MyGuild.DeleteMember(this, Name);
                        break;

                    case "CREATEGUILD":

                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        player = parts.Length < 3 ? this : Envir.GetPlayer(parts[1]);

                        if (player == null)
                        {
                            ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                            return;
                        }

                        if (player.MyGuild != null)
                        {
                            ReceiveChat(LanguageMgr.Get("PlayerAlreadyInGuild", player.Name), ChatType.System);
                            return;
                        }

                        String gName = parts.Length < 3 ? parts[1] : parts[2];
                        if ((gName.Length < 3) || (gName.Length > 20))
                        {
                            ReceiveChat(LanguageMgr.Get("GuildNameRestrictedLength"), ChatType.System);
                            return;
                        }

                        GuildObject guild = Envir.GetGuild(gName);
                        if (guild != null)
                        {
                            ReceiveChat(LanguageMgr.Get("GuildAlreadyExists", gName), ChatType.System);
                            return;
                        }

                        player.CanCreateGuild = true;
                        if (player.CreateGuild(gName))
                        {
                            ReceiveChat(LanguageMgr.Get("SuccessfullyCreatedGuild", gName), ChatType.System);
                        }
                        else
                        {
                            ReceiveChat(LanguageMgr.Get("FailedToCreateGuild"), ChatType.System);
                        }

                        player.CanCreateGuild = false;
                        break;

                    case "ALLOWTRADE":
                        AllowTrade = !AllowTrade;

                        if (AllowTrade)
                            ReceiveChat(LanguageMgr.Get("AllowTradeNow"), ChatType.System);
                        else
                            ReceiveChat(LanguageMgr.Get("NoLongerAllowTrade"), ChatType.System);
                        break;

                    case "TRIGGER":
                        if (!IsGM) return;
                        if (parts.Length < 2) return;

                        if (parts.Length >= 3)
                        {
                            player = Envir.GetPlayer(parts[2]);

                            if (player == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[2]), ChatType.System);
                                return;
                            }

                            player.CallDefaultNPC(DefaultNPCType.Trigger, parts[1]);
                            return;
                        }

                        foreach (var pl in Envir.Players)
                        {
                            pl.CallDefaultNPC(DefaultNPCType.Trigger, parts[1]);
                        }

                        break;

                    case "RIDE":
                        ToggleRide();

                        if (HasHero && HeroSpawned && Hero.RidingMount != RidingMount)
                            Hero.ToggleRide();

                        ChatTime = 0;
                        break;
                    case "SETFLAG":
                        if (!IsGM && !Settings.TestServer) return;

                        if (parts.Length < 2) return;

                        int tempInt = 0;

                        if (!int.TryParse(parts[1], out tempInt)) return;

                        if (tempInt > Info.Flags.Length - 1) return;

                        Info.Flags[tempInt] = !Info.Flags[tempInt];

                        for (int f = CurrentMap.NPCs.Count - 1; f >= 0; f--)
                        {
                            if (Functions.InRange(CurrentMap.NPCs[f].CurrentLocation, CurrentLocation, Globals.DataRange))
                                CurrentMap.NPCs[f].CheckVisible(this);
                        }

                        break;

                    case "LISTFLAGS":
                        if (!IsGM && !Settings.TestServer) return;

                        for (int i = 0; i < Info.Flags.Length; i++)
                        {
                            if (Info.Flags[i] == false) continue;

                            ReceiveChat(LanguageMgr.Get("Flag", i), ChatType.System);
                        }
                        break;

                    case "CLEARFLAGS":
                        if (!IsGM && !Settings.TestServer) return;

                        player = parts.Length > 1 && IsGM ? Envir.GetPlayer(parts[1]) : this;

                        if (player == null)
                        {
                            ReceiveChat(LanguageMgr.Get("PlayerIsNotOnline", parts[1]), ChatType.System);
                            return;
                        }

                        for (int i = 0; i < player.Info.Flags.Length; i++)
                        {
                            player.Info.Flags[i] = false;
                        }
                        break;
                    case "CLEARMOB":
                        if (!IsGM) return;

                        if (parts.Length > 1)
                        {
                            map = Envir.GetMapByNameAndInstance(parts[1]);

                            if (map == null) return;

                        }
                        else
                        {
                            map = CurrentMap;
                        }

                        foreach (var cell in map.Cells)
                        {
                            if (cell == null || cell.Objects == null) continue;

                            int obCount = cell.Objects.Count();

                            for (int m = 0; m < obCount; m++)
                            {
                                MapObject ob = cell.Objects[m];

                                if (ob.Race != ObjectType.Monster) continue;
                                if (ob.Dead) continue;
                                ob.Die();
                            }
                        }

                        break;

                    case "CHANGECLASS": //@changeclass [Player] [Class]
                        if (!IsGM && !Settings.TestServer) return;

                        data = parts.Length <= 2 || !IsGM ? Info : Envir.GetCharacterInfo(parts[1]);

                        if (data == null) return;

                        MirClass mirClass;

                        if (!Enum.TryParse(parts[parts.Length - 1], true, out mirClass) || data.Class == mirClass) return;

                        data.Class = mirClass;

                        ReceiveChat(LanguageMgr.Get("PlayerChangedTo", data.Name, data.Class), ChatType.System);
                        MessageQueue.Enqueue(string.Format("Player {0} has been changed to {1} by {2}", data.Name, data.Class, Name));

                        Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("ClassChangedByGM", data.Player.Name, data.Class.ToString(), Name));

                        if (data.Player != null)
                            data.Player.Connection.LogOut();
                        break;

                    case "DIE":
                        LastHitter = null;
                        Die();
                        break;
                    case "HAIR":
                        if (!IsGM && !Settings.TestServer) return;

                        if (parts.Length < 2)
                        {
                            Info.Hair = (byte)Envir.Random.Next(0, 9);
                        }
                        else
                        {
                            byte tempByte = 0;

                            byte.TryParse(parts[1], out tempByte);

                            Info.Hair = tempByte;
                        }
                        break;

                    case "DECO":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        int.TryParse(parts[1], out tempInt);

                        DecoObject decoOb = new DecoObject
                        {
                            Image = tempInt,
                            CurrentMap = CurrentMap,
                            CurrentLocation = CurrentLocation,
                        };

                        CurrentMap.AddObject(decoOb);
                        decoOb.Spawned();

                        Enqueue(decoOb.GetInfo());

                        break;

                    case "ADJUSTPKPOINT":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                        if (parts.Length > 2)
                        {
                            if (!IsGM) return;

                            player = Envir.GetPlayer(parts[1]);

                            if (player == null) return;


                            int.TryParse(parts[2], out tempInt);
                        }
                        else
                        {
                            player = this;
                            int.TryParse(parts[1], out tempInt);
                        }

                        player.PKPoints = tempInt;

                        break;

                    case "AWAKENING":
                        {
                            if ((!IsGM && !Settings.TestServer) || parts.Length < 3) return;

                            ItemType type;

                            if (!Enum.TryParse(parts[1], true, out type)) return;

                            AwakeType awakeType;

                            if (!Enum.TryParse(parts[2], true, out awakeType)) return;

                            foreach (UserItem temp in Info.Equipment)
                            {
                                if (temp == null) continue;

                                ItemInfoTable realItem = Functions.GetRealItem(temp.Info, Info.Level, Info.Class, Envir.ItemInfoList);

                                if (realItem.Type == type)
                                {
                                    Awake awake = temp.Awake;
                                    bool[] isHit;
                                    int result = awake.UpgradeAwake(temp, awakeType, out isHit);
                                    switch (result)
                                    {
                                        case -1:
                                            ReceiveChat(LanguageMgr.Get("ConditionError", temp.FriendlyName), ChatType.System);
                                            break;
                                        case 0:
                                            ReceiveChat(LanguageMgr.Get("UpgradeFailed", temp.FriendlyName), ChatType.System);
                                            break;
                                        case 1:
                                            ReceiveChat(LanguageMgr.Get("AwakeLevelValue", temp.FriendlyName, awake.GetAwakeLevel(), awake.GetAwakeValue(), awake.GetAwakeValue()), ChatType.System);
                                            p = new S.RefreshItem { Item = temp };
                                            Enqueue(p);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                        }
                        break;
                    case "REMOVEAWAKENING":
                        {
                            if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;

                            ItemType type;

                            if (!Enum.TryParse(parts[1], true, out type)) return;

                            foreach (UserItem temp in Info.Equipment)
                            {
                                if (temp == null) continue;

                                ItemInfoTable realItem = Functions.GetRealItem(temp.Info, Info.Level, Info.Class, Envir.ItemInfoList);

                                if (realItem.Type == type)
                                {
                                    Awake awake = temp.Awake;
                                    int result = awake.RemoveAwake();
                                    switch (result)
                                    {
                                        case 0:
                                            ReceiveChat(LanguageMgr.Get("PlayerRemoveFailedLevel0", temp.FriendlyName), ChatType.System);
                                            break;
                                        case 1:
                                            ReceiveChat(LanguageMgr.Get("PlayerRemoveSuccessLevel", temp.FriendlyName, temp.Awake.GetAwakeLevel()), ChatType.System);
                                            p = new S.RefreshItem { Item = temp };
                                            Enqueue(p);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                        }
                        break;

                    case "STARTWAR":
                        if (!IsGM) return;
                        if (parts.Length < 2) return;

                        GuildObject enemyGuild = Envir.GetGuild(parts[1]);

                        if (MyGuild == null)
                        {
                            ReceiveChat(LanguageMgr.Get("NotInGuild"), ChatType.System);
                        }

                        if (MyGuild.Ranks[0] != MyGuildRank)
                        {
                            ReceiveChat(LanguageMgr.Get("YouMustBeLeaderToStartWar"), ChatType.System);
                            return;
                        }

                        if (enemyGuild == null)
                        {
                            ReceiveChat(LanguageMgr.Get("CouldNotFindGuild", parts[1]), ChatType.System);
                            return;
                        }

                        if (MyGuild == enemyGuild)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotWarOwnGuild"), ChatType.System);
                            return;
                        }

                        if (enemyGuild.Name == Settings.NewbieGuild)
                        {
                            ReceiveChat(LanguageMgr.Get("CannotWarWithNewPlayersGuild"), ChatType.System);
                            return;
                        }

                        if (MyGuild.WarringGuilds.Contains(enemyGuild))
                        {
                            ReceiveChat(LanguageMgr.Get("AlreadyAtWarWithGuild"), ChatType.System);
                            return;
                        }

                        if (MyGuild.GoToWar(enemyGuild))
                        {
                            ReceiveChat(LanguageMgr.Get("YouStartedWarWith", parts[1]), ChatType.System);
                            enemyGuild.SendMessage(LanguageMgr.Get("HasStartedWar", MyGuild.Name), ChatType.System);
                        }
                        break;
                    case "ADDINVENTORY":
                        {
                            if (Info.Inventory.Length >= InventoryLayout.MaxSlots)
                            {
                                ReceiveChat("背包格子已全部解锁。", ChatType.System);
                                ChatTime = 0;
                                break;
                            }

                            int openLevel = (Info.Inventory.Length - InventoryLayout.InitialSlots) / InventoryLayout.InventoryExpandStep;
                            uint openGold = (uint)(InventoryLayout.InventoryExpandBaseGold * (1 + openLevel));
                            if (Info.Gold >= openGold)
                            {
                                Info.Gold -= openGold;
                                Enqueue(new S.LoseGold { Gold = openGold });
                                Enqueue(new S.ResizeInventory { Size = Info.ResizeInventory() });
                                ReceiveChat(LanguageMgr.Get("InventoryIncreased"), ChatType.System);
                            }
                            else
                            {
                                ReceiveChat(LanguageMgr.Get("LowGold"), ChatType.System);
                            }
                            ChatTime = 0;
                        }
                        break;

                    case "ADDSTORAGE":
                        {
                            TimeSpan addedTime = new TimeSpan(10, 0, 0, 0);
                            uint cost = 1000000;

                            if (Info.Gold >= cost)
                            {
                                var sz = Account.ExpandStorage();

                                Info.Gold -= cost;
                                Account.HasExpandedStorage = true;

                                if (Account.ExpandedStorageExpiryDate > Envir.Now)
                                {
                                    Account.ExpandedStorageExpiryDate = Account.ExpandedStorageExpiryDate + addedTime;
                                    ReceiveChat(LanguageMgr.Get("ExpandedStorageExpiresOn")+ Account.ExpandedStorageExpiryDate.ToString(), ChatType.System);
                                }
                                else
                                {
                                    Account.ExpandedStorageExpiryDate = Envir.Now + addedTime;
                                    ReceiveChat(LanguageMgr.Get("ExpandedStorageExpiresOn")+ Account.ExpandedStorageExpiryDate.ToString(), ChatType.System);
                                }

                                Enqueue(new S.LoseGold { Gold = cost });
                                Enqueue(new S.ResizeStorage { Size = sz, HasExpandedStorage = Account.HasExpandedStorage, ExpiryTime = Account.ExpandedStorageExpiryDate });
                            }
                            else
                            {
                                ReceiveChat(LanguageMgr.Get("LowGold"), ChatType.System);
                            }
                            ChatTime = 0;
                        }
                        break;

                    case "SUMMONHERO":
                        {
                            if (!HasHero) return;

                        if (!HeroSpawned)
                                SummonHero();
                        else if (Hero != null)
                        {
                            long remaining = Hero.LogTime - Envir.Time;
                            if (remaining > 0)
                            {
                                int remainingSeconds = (int)Math.Ceiling(remaining / 1000D);
                                ReceiveChat(LanguageMgr.Get("HeroDesummonCountdown", remainingSeconds), ChatType.System);
                                return;
                            }

                            DespawnHero();
                            Info.HeroSpawned = false;
                        }
                        }
                        break;

                    case "ALLOWOBSERVE":
                        AllowObserve = !AllowObserve;
                        Enqueue(new S.AllowObserve { Allow = AllowObserve });
                        break;

                    case "INFO":
                        {
                            if (!IsGM && !Settings.TestServer) return;

                            MapObject ob = null;

                            if (parts.Length < 2)
                            {
                                Point target = Functions.PointMove(CurrentLocation, Direction, 1);
                                if (!CurrentMap.ValidPoint(target)) return;
                                Cell cell = CurrentMap.GetCell(target);

                                if (cell.Objects == null || cell.Objects.Count < 1) return;

                                ob = cell.Objects[0];
                            }
                            else
                            {
                                ob = Envir.GetPlayer(parts[1]);
                            }

                            if (ob == null) return;

                            switch (ob.Race)
                            {
                                case ObjectType.Player:
                                    PlayerObject plOb = (PlayerObject)ob;
                                    ReceiveChat(LanguageMgr.Get("PlayerInfoTitle"), ChatType.System);
                                    ReceiveChat(LanguageMgr.Get("PlayerInfo", plOb.Name, plOb.Level, plOb.CurrentLocation.X, plOb.CurrentLocation.Y), ChatType.System);
                                    break;
                                case ObjectType.Monster:
                                    MonsterObject monOb = (MonsterObject)ob;
                                    ReceiveChat(LanguageMgr.Get("MonsterInfoTitle"), ChatType.System);
                                    ReceiveChat(LanguageMgr.Get("MonsterInfo1", monOb.Info.Index, monOb.Name), ChatType.System);
                                    ReceiveChat(LanguageMgr.Get("MonsterInfo2", monOb.Level, monOb.CurrentLocation.X, monOb.CurrentLocation.Y, monOb.Direction), ChatType.System);
                                    ReceiveChat(LanguageMgr.Get("MonsterInfo3", monOb.Info.Stats[Stat.HP], monOb.Stats[Stat.MinDC], monOb.Stats[Stat.MaxDC]), ChatType.System);
                                    break;
                                case ObjectType.Merchant:
                                    NPCObject npcOb = (NPCObject)ob;
                                    ReceiveChat(LanguageMgr.Get("NpcInfoTitle"), ChatType.System);
                                    ReceiveChat(LanguageMgr.Get("NpcInfo1", npcOb.Info.Index, npcOb.Name), ChatType.System);
                                    ReceiveChat(LanguageMgr.Get("NpcInfo2", ob.CurrentLocation.X, ob.CurrentLocation.Y), ChatType.System);
                                    ReceiveChat(LanguageMgr.Get("NpcInfo3", npcOb.Info.FileName), ChatType.System);
                                    break;
                            }
                        }
                        break;

                    case "CLEARQUESTS":
                        if (!IsGM && !Settings.TestServer) return;

                        player = parts.Length > 1 && IsGM ? Envir.GetPlayer(parts[1]) : this;

                        if (player == null)
                        {
                            ReceiveChat(LanguageMgr.Get("PlayerIsNotOnline", parts[1]), ChatType.System);
                            return;
                        }

                        // 进行中任务：必须用 player.SendUpdateQuest，否则包发给 GM 自己而非目标玩家
                        for (int i = player.CurrentQuests.Count - 1; i >= 0; i--)
                        {
                            player.SendUpdateQuest(player.CurrentQuests[i], QuestState.Remove);
                        }

                        // 追踪栏：清掉左侧任务跟踪列表
                        player.TrackedQuests.Clear();

                        // 已开放（未接取）任务：清服务端列表，并给客户端发 Remove 同步（否则追踪栏仍显示“寻找 NPC”）
                        foreach (int offeredIndex in player.OfferedQuests)
                            player.Enqueue(new S.ChangeQuest { Quest = new ClientQuestProgress { Id = offeredIndex }, QuestState = QuestState.Remove });
                        player.OfferedQuests.Clear();

                        // 完成记录：清服务端并下发空完成列表
                        player.CompletedQuests.Clear();
                        player.GetCompletedQuests();

                        // 任务道具：清掉已无任务引用的任务背包物品（客户端同步删除）
                        player.RecalculateQuestBag();

                        break;

                    case "SETQUEST":
                        if ((!IsGM && !Settings.TestServer) || parts.Length < 3) return;

                        player = parts.Length > 3 && IsGM ? Envir.GetPlayer(parts[3]) : this;

                        if (player == null)
                        {
                            ReceiveChat(LanguageMgr.Get("PlayerIsNotOnline", parts[3]), ChatType.System);
                            return;
                        }

                        int.TryParse(parts[1], out int questID);
                        int.TryParse(parts[2], out int questState);

                        if (questID < 1) return;

                        var activeQuest = player.CurrentQuests.FirstOrDefault(e => e.Index == questID);

                        //remove from active list
                        if (activeQuest != null)
                        {
                            player.SendUpdateQuest(activeQuest, QuestState.Remove);
                            player.TrackedQuests.Remove(questID);
                        }

                        switch (questState)
                        {
                            case 0: //cancel
                                if (player.CompletedQuests.Contains(questID))
                                {
                                    player.CompletedQuests.Remove(questID);
                                }
                                break;
                            case 1: //complete
                                if (!player.CompletedQuests.Contains(questID))
                                {
                                    player.CompletedQuests.Add(questID);
                                }
                                break;
                        }

                        player.GetCompletedQuests();
                        break;

                    case "TOGGLETRANSFORM":
                        if (HasBuff(BuffType.Transform, out Buff transform))
                        {
                            if (transform.Paused)
                            {
                                UnpauseBuff(transform);
                            }
                            else
                            {
                                PauseBuff(transform);
                            }
                            RefreshStats();

                            hintstring = transform.Paused ? LanguageMgr.Get("TransformDisabled"): LanguageMgr.Get("TransformEnabled");
                            ReceiveChat(hintstring, ChatType.System);
                        }
                        break;

                    case "STARTCONQUEST":
                        {
                            if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;
                            int conquestID;

                            if (parts.Length < 1)
                            {
                                ReceiveChat(LanguageMgr.Get("SyntaxStartConquest"), ChatType.System);
                                return;
                            }

                            if (MyGuild == null)
                            {
                                ReceiveChat(LanguageMgr.Get("NeedGuildToStartWar"), ChatType.System);
                                return;
                            }

                            else if (!int.TryParse(parts[1], out conquestID)) return;

                            ConquestObject tempConq = Envir.Conquests.FirstOrDefault(t => t.Info.Index == conquestID);

                            if (tempConq != null)
                            {
                                tempConq.StartType = ConquestType.Forced;
                                tempConq.WarIsOn = !tempConq.WarIsOn;
                                tempConq.GuildInfo.AttackerID = MyGuild.Guildindex;
                            }
                            else return;
                            ReceiveChat(LanguageMgr.Get("WarStarted", tempConq.Info.Name), ChatType.System);
                            MessageQueue.Enqueue(string.Format("{0} War Started.", tempConq.Info.Name));

                            foreach (var pl in Envir.Players)
                            {
                                if (tempConq.WarIsOn)
                                {
                                    pl.ReceiveChat(LanguageMgr.Get("WarStarted", tempConq.Info.Name), ChatType.System);
                                }
                                else
                                {
                                    pl.ReceiveChat(LanguageMgr.Get("WarStopped", tempConq.Info.Name), ChatType.System);
                                }

                                pl.BroadcastInfo();
                            }
                        }
                        break;
                    case "RESETCONQUEST":
                        {
                            if ((!IsGM && !Settings.TestServer) || parts.Length < 2) return;
                            int conquestID;

                            if (parts.Length < 1)
                            {
                                ReceiveChat(LanguageMgr.Get("SyntaxResetConquest"), ChatType.System);
                                return;
                            }

                            if (MyGuild == null)
                            {
                                ReceiveChat(LanguageMgr.Get("NeedGuildToStartWar"), ChatType.System);
                                return;
                            }

                            else if (!int.TryParse(parts[1], out conquestID)) return;

                            ConquestObject resetConq = Envir.Conquests.FirstOrDefault(t => t.Info.Index == conquestID);

                            if (resetConq != null && !resetConq.WarIsOn)
                            {
                                resetConq.Reset();
                                ReceiveChat(LanguageMgr.Get("HasBeenReset", resetConq.Info.Name), ChatType.System);
                            }
                            else
                            {
                                ReceiveChat(LanguageMgr.Get("ConquestNotFoundOrWarOn"), ChatType.System);
                            }
                        }
                        break;
                    case "GATES":
                        if (MyGuild == null || MyGuild.Conquest == null || !MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeRank) || MyGuild.Conquest.WarIsOn)
                        {
                            ReceiveChat(LanguageMgr.Get("NoAccessControlGates"), ChatType.System);
                            return;
                        }

                        bool openClose = false;

                        if (parts.Length > 1)
                        {
                            string openclose = parts[1];

                            if (openclose.ToUpper() == "CLOSE")
                            {
                                openClose = true;
                            }
                            else if (openclose.ToUpper() == "OPEN")
                            {
                                openClose = false;
                            }
                            else
                            {
                                ReceiveChat(LanguageMgr.Get("MustTypeGatesCommand"), ChatType.System);
                                return;
                            }

                            for (int i = 0; i < MyGuild.Conquest.GateList.Count; i++)
                            {
                                if (MyGuild.Conquest.GateList[i].Gate != null && !MyGuild.Conquest.GateList[i].Gate.Dead)
                                {
                                    if (openClose)
                                    {
                                        MyGuild.Conquest.GateList[i].Gate.CloseDoor();
                                    }
                                    else
                                    {
                                        MyGuild.Conquest.GateList[i].Gate.OpenDoor();
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < MyGuild.Conquest.GateList.Count; i++)
                            {
                                if (MyGuild.Conquest.GateList[i].Gate != null && !MyGuild.Conquest.GateList[i].Gate.Dead)
                                {
                                    if (!MyGuild.Conquest.GateList[i].Gate.Closed)
                                    {
                                        MyGuild.Conquest.GateList[i].Gate.CloseDoor();
                                        openClose = true;
                                    }
                                    else
                                    {
                                        MyGuild.Conquest.GateList[i].Gate.OpenDoor();
                                        openClose = false;
                                    }
                                }
                            }
                        }

                        if (openClose)
                        {
                            ReceiveChat(LanguageMgr.Get("GatesClosed", MyGuild.Conquest.Info.Name), ChatType.System);
                        }
                        else
                        {
                            ReceiveChat(LanguageMgr.Get("GatesOpenedAt", MyGuild.Conquest.Info.Name), ChatType.System);
                        }
                        break;

                    case "CHANGEFLAG":
                        if (MyGuild == null || MyGuild.Conquest == null || !MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeRank) || MyGuild.Conquest.WarIsOn)
                        {
                            ReceiveChat(LanguageMgr.Get("NoAccessChangeFlags"), ChatType.System);
                            return;
                        }

                        ushort flag = (ushort)Envir.Random.Next(12);

                        if (parts.Length > 1)
                        {
                            ushort.TryParse(parts[1], out ushort temp);

                            if (temp <= 11) flag = temp;
                        }

                        MyGuild.Info.FlagImage = (ushort)(1000 + flag);

                        for (int i = 0; i < MyGuild.Conquest.FlagList.Count; i++)
                        {
                            MyGuild.Conquest.FlagList[i].UpdateImage();
                        }

                        break;
                    case "CHANGEFLAGCOLOUR":
                        {
                            if (MyGuild == null || MyGuild.Conquest == null || !MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeRank) || MyGuild.Conquest.WarIsOn)
                            {
                                ReceiveChat(LanguageMgr.Get("NoAccessChangeFlags"), ChatType.System);
                                return;
                            }

                            byte r1 = (byte)Envir.Random.Next(255);
                            byte g1 = (byte)Envir.Random.Next(255);
                            byte b1 = (byte)Envir.Random.Next(255);

                            if (parts.Length > 3)
                            {
                                byte.TryParse(parts[1], out r1);
                                byte.TryParse(parts[2], out g1);
                                byte.TryParse(parts[3], out b1);
                            }

                            MyGuild.Info.FlagColour = Color.FromArgb(255, r1, g1, b1);

                            for (int i = 0; i < MyGuild.Conquest.FlagList.Count; i++)
                            {
                                MyGuild.Conquest.FlagList[i].UpdateColour();
                            }
                        }
                        break;
                    case "REVIVE":
                        if (!IsGM) return;

                        if (parts.Length < 2)
                        {
                            RefreshStats();
                            SetHP(Stats[Stat.HP]);
                            SetMP(Stats[Stat.MP]);
                            Revive(MaxHealth, true);
                        }
                        else
                        {
                            player = Envir.GetPlayer(parts[1]);
                            if (player == null) return;

                            player.Revive(MaxHealth, true);

                            Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("RevivedToFullHealthByGM", player.Name, Name));
                        }
                        break;
                    case "DELETESKILL":
                        if ((!IsGM) || parts.Length < 2) return;
                        Spell skill1;

                        if (!Enum.TryParse(parts.Length > 2 ? parts[2] : parts[1], true, out skill1)) return;

                        if (skill1 == Spell.无) return;

                        if (parts.Length > 2)
                        {
                            if (!IsGM) return;
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(LanguageMgr.Get("PlayerNotFound", parts[1]), ChatType.System);
                                return;
                            }
                        }
                        else
                        {
                            player = this;
                        }

                        if (player == null) return;

                        var magics = new MagicInfoRunItem(skill1);
                        bool removed = false;

                        for (var i = player.Info.Magics.Count - 1; i >= 0; i--)
                        {
                            if (player.Info.Magics[i].Spell != skill1) continue;

                            player.Info.Magics.RemoveAt(i);
                            player.Enqueue(new S.RemoveMagic { PlaceId = i });
                            removed = true;
                        }

                        if (removed)
                        {
                            ReceiveChat(LanguageMgr.Get("DeletedSkillFromPlayer", skill1.ToString(), player.Name), ChatType.System);
                            player.ReceiveChat(LanguageMgr.Get("ItemRemovedFromYou", skill1), ChatType.System);

                            Helpers.ChatSystem.SystemMessage(chatMessage: LanguageMgr.Get("SkillRemovedByGM", player, skill1.ToString(), Name));
                        }
                        else
                        {
                            ReceiveChat(LanguageMgr.Get("UnableDeleteSkillNotFound"), ChatType.System);
                        }

                        break;
                    case "SETTIMER":
                        if (parts.Length < 4) return;

                        string key = parts[1];

                        if (!int.TryParse(parts[2], out int seconds)) return;
                        if (!byte.TryParse(parts[3], out byte timerType)) return;

                        SetTimer(key, seconds, timerType);

                        break;
                    case "SETLIGHT":
                        if ((!IsGM) || parts.Length < 2) return;

                        if (!byte.TryParse(parts[1], out byte light)) return;

                        Light = light;

                        Enqueue(GetUpdateInfo());
                        Broadcast(GetUpdateInfo());
                        break;
                    default:
                        break;
                }

                foreach (string command in Envir.CustomCommands)
                {
                    if (string.Compare(parts[0], command, true) != 0) continue;

                    CallDefaultNPC(DefaultNPCType.CustomCommand, parts[0]);
                }
            }
        }
    }
}
