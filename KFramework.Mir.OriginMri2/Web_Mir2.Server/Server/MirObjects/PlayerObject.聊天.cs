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
        public void Chat(string message, List<ChatItem> linkedItems = null, ChatType channel = ChatType.Nearby, uint privateTargetId = 0)
        {
            if (string.IsNullOrEmpty(message)) return;

            MessageQueue.EnqueueChat(string.Format("{0}: {1}", Name, message));

            if (GMLogin)
            {
                if (message == GMPassword)
                {
                    IsGM = true;
                    UpdateGMBuff();
                    MessageQueue.Enqueue(string.Format("{0} is now a GM", Name));
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouHaveBeenMadeGM), ChatType.System);
                    Envir.RemoveRank(Info);//remove gm chars from ranking to avoid causing bugs in rank list
                }
                else
                {
                    MessageQueue.Enqueue(string.Format("{0} attempted a GM login", Name));
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.LoginPasswordInvalid), ChatType.System);
                }
                GMLogin = false;
                return;
            }

            if (Info.ChatBanned)
            {
                if (Info.ChatBanExpiryDate > Envir.Now)
                {
                    TimeSpan chatBanRemaining = Info.ChatBanExpiryDate - Envir.Now;

                    if (chatBanRemaining.Days > 0)
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ChatBanRemainingTimeByDay), chatBanRemaining.Days, chatBanRemaining.Hours, chatBanRemaining.Minutes, chatBanRemaining.Seconds), ChatType.System);
                    else if (chatBanRemaining.Hours > 0)
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ChatBanRemainingTimeByHour), chatBanRemaining.Hours, chatBanRemaining.Minutes, chatBanRemaining.Seconds), ChatType.System);
                    else if (chatBanRemaining.Minutes > 0)
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ChatBanRemainingTimeByMinutes), chatBanRemaining.Minutes, chatBanRemaining.Seconds), ChatType.System);
                    else
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ChatBanRemainingTimeBySecond), chatBanRemaining.Seconds), ChatType.System);
                    return;
                }

                Info.ChatBanned = false;
            }
            else
            {
                if (ChatTime > Envir.Time)
                {
                    if (ChatTick >= 5 & !IsGM)
                    {
                        Info.ChatBanned = true;
                        Info.ChatBanExpiryDate = Envir.Now.AddMinutes(5);
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.ChatBanDuration5Minutes), ChatType.System);
                        return;
                    }

                    ChatTick++;
                }
                else
                    ChatTick = 0;

                ChatTime = Envir.Time + 2000;
            }

            string[] parts = null;

            message = message.Replace("$pos", Functions.PointToString(CurrentLocation));

            Packet p;

            // GM 命令以 @ 前缀标识,与频道无关:统一转交 GMCommand 处理(等价于原 if-else 链末尾的 @!/@ 分支)
            if (message.StartsWith("@"))
            {
                GMCommand(message, linkedItems);
                return;
            }

            switch (channel)
            {
                case ChatType.PrivateChat:
                    {
                        // 师徒私聊
                        if (message.StartsWith("!#"))
                        {
                            //Mentor Message
                            message = message.Remove(0, 2);
                            parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length == 0) return;

                            if (Info.Mentor == 0) return;

                            CharacterInfo Mentor = Envir.GetCharacterInfo(Info.Mentor);
                            PlayerObject player = Envir.GetPlayer(Mentor.Name);

                            if (player == null)
                            {
                                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TargetIsNotOnline), Mentor.Name), ChatType.System);
                                return;
                            }

                            message = ProcessChatItems(message, new List<PlayerObject> { player }, linkedItems);

                            ReceiveChat(string.Format("{0}: {1}", Name, message), ChatType.PrivateChat);
                            player.ReceiveChat(string.Format("{0}: {1}", Name, message), ChatType.PrivateChat);
                            break;
                        }

                        // 伴侣私聊
                        if (message.StartsWith(":)"))
                        {
                            //Relationship Message
                            message = message.Remove(0, 2);
                            parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length == 0) return;

                            if (Info.Married == 0) return;

                            CharacterInfo Lover = Envir.GetCharacterInfo(Info.Married);
                            PlayerObject player = Envir.GetPlayer(Lover.Name);

                            if (player == null)
                            {
                                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TargetIsNotOnline), Lover.Name), ChatType.System);
                                return;
                            }

                            message = ProcessChatItems(message, new List<PlayerObject> { player }, linkedItems);

                            ReceiveChat(string.Format("{0}: {1}", Name, message), ChatType.PrivateChat);
                            player.ReceiveChat(string.Format("{0}: {1}", Name, message), ChatType.PrivateChat);
                            break;
                        }

                        // 普通私聊:按 privateTargetId 精确投递 或 按 /名字 兜底
                        {
                            PlayerObject player = null;

                            if (privateTargetId != 0)
                            {
                                // 按显式角色ID精确投递
                                CharacterInfo targetInfo = Envir.GetCharacterInfo((int)privateTargetId);
                                if (targetInfo != null)
                                    player = Envir.GetPlayer(targetInfo.Name);

                                if (player == null)
                                {
                                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CouldNotFind), targetInfo != null ? targetInfo.Name : privateTargetId.ToString()), ChatType.System);
                                    return;
                                }
                            }
                            else
                            {
                                if (message.StartsWith("/")) message = message.Remove(0, 1);
                                parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                                if (parts.Length == 0) return;

                                player = Envir.GetPlayer(parts[0]);

                                if (player == null)
                                {
                                    IntelligentCreatureObject creature = GetCreatureByName(parts[0]);
                                    if (creature != null)
                                    {
                                        creature.ReceiveChat(message.Remove(0, parts[0].Length), ChatType.PrivateChat);
                                        return;
                                    }
                                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CouldNotFind), parts[0]), ChatType.System);
                                    return;
                                }
                            }

                            if (player.Info.Friends.Any(e => e.Info == Info && e.Blocked))
                            {
                                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.PlayerNotAcceptingMessages), ChatType.System);
                                return;
                            }

                            if (Info.Friends.Any(e => e.Info == player.Info && e.Blocked))
                            {
                                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotMessageBlacklistedPlayer), ChatType.System);
                                return;
                            }

                            message = ProcessChatItems(message, new List<PlayerObject> { player }, linkedItems);

                            ReceiveChat(string.Format("/{0}", message), ChatType.PrivateChat);
                            string recipientContent = (privateTargetId == 0 && parts != null && parts.Length > 0)
                                ? message.Remove(0, parts[0].Length)
                                : message;
                            player.ReceiveChat(string.Format("{0}=>{1}", Name, recipientContent), ChatType.PrivateChat);
                        }
                    }
                    break;
                case ChatType.Team:
                    {
                        if (GroupMembers == null) return;
                        //Group
                        if (message.StartsWith("!!")) message = message.Remove(0, 2);
                        message = String.Format("{0}:{1}", Name, " " + message);

                        message = ProcessChatItems(message, GroupMembers, linkedItems);

                        p = new S.ObjectChat { ObjectID = ObjectID, Text = message, Type = ChatType.Team };

                        for (int i = 0; i < GroupMembers.Count; i++)
                            GroupMembers[i].Enqueue(p);
                    }
                    break;
                case ChatType.Guild:
                    {
                        if (MyGuild == null) return;

                        //Guild
                        if (message.StartsWith("!~")) message = message.Remove(0, 2);
                        message = ProcessChatItems(message, MyGuild.GetOnlinePlayers(), linkedItems);

                        MyGuild.SendMessage(String.Format("{0}: {1}", Name, message));
                    }
                    break;
                case ChatType.World:
                    {
                        //Shout
                        if (Envir.Time < ShoutTime)
                        {
                            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CannotShoutForSeconds), Math.Ceiling((ShoutTime - Envir.Time) / 1000D)), ChatType.System);
                            break;
                        }
                        if (Level < 8 && (!HasMapShout && !HasServerShout))
                        {
                            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.RequiredLevel8ForShout), ChatType.System);
                            break;
                        }

                        ShoutTime = Envir.Time + 10000;
                        if (message.StartsWith("!")) message = message.Remove(0, 1);
                        message = String.Format("(!){0}:{1}", Name, message);

                        if (HasMapShout)
                        {
                            message = ProcessChatItems(message, CurrentMap.Players, linkedItems);

                            p = new S.Chat { Message = message, Type = ChatType.World };
                            HasMapShout = false;

                            for (int i = 0; i < CurrentMap.Players.Count; i++)
                            {
                                CurrentMap.Players[i].Enqueue(p);
                            }
                            break;
                        }
                        else if (HasServerShout)
                        {
                            message = ProcessChatItems(message, Envir.Players, linkedItems);

                            p = new S.Chat { Message = message, Type = ChatType.World };
                            HasServerShout = false;

                            for (int i = 0; i < Envir.Players.Count; i++)
                            {
                                Envir.Players[i].Enqueue(p);
                            }
                            break;
                        }
                        else
                        {
                            List<PlayerObject> playersInRange = new List<PlayerObject>();

                            for (int i = 0; i < CurrentMap.Players.Count; i++)
                            {
                                if (!Functions.InRange(CurrentLocation, CurrentMap.Players[i].CurrentLocation, Globals.DataRange * 2)) continue;

                                playersInRange.Add(CurrentMap.Players[i]);
                            }

                            message = ProcessChatItems(message, playersInRange, linkedItems);

                            p = new S.Chat { Message = " " + message, Type = ChatType.World };

                            for (int i = 0; i < playersInRange.Count; i++)
                            {
                                playersInRange[i].Enqueue(p);
                            }
                        }
                    }
                    break;
                default:
                    {
                        //Nearby
                        message = String.Format("{0}:{1}", CurrentMap.Info.NoNames ? "?????" : Name, " " + message);

                        message = ProcessChatItems(message, null, linkedItems);

                        p = new S.ObjectChat { ObjectID = ObjectID, Text = message, Type = ChatType.Nearby };

                        Enqueue(p);
                        Broadcast(p);
                    }
                    break;
            }
        }

        private string ProcessChatItems(string text, List<PlayerObject> recipients, List<ChatItem> chatItems)
        {
            if (chatItems == null)
            {
                return text;
            }

            foreach (var chatItem in chatItems)
            {
                Regex r = new Regex(chatItem.RegexInternalName, RegexOptions.IgnoreCase);

                text = r.Replace(text, chatItem.InternalName, 1);

                UserItem[] array;

                switch (chatItem.Grid)
                {
                    case MirGridType.Inventory:
                        array = Info.Inventory;
                        break;
                    case MirGridType.Storage:
                        if (!CanAccessStorage()) return text;
                        array = Info.AccountInfo.Storage;
                        break;
                    case MirGridType.HeroInventory:
                        if (!HasHero || !HeroSpawned)
                            return text;
                        array = CurrentHero.Inventory;
                        break;
                    default:
                        continue;
                }

                UserItem item = null;

                for (int i = 0; i < array.Length; i++)
                {
                    item = array[i];
                    if (item == null || item.UniqueID != chatItem.UniqueID) continue;
                    break;
                }

                if (item != null)
                {
                    if (recipients == null)
                    {
                        for (int i = CurrentMap.Players.Count - 1; i >= 0; i--)
                        {
                            PlayerObject player = CurrentMap.Players[i];
                            if (player == this) continue;

                            if (player == null || player.Info == null || player.Node == null) continue;

                            if (Functions.InRange(CurrentLocation, player.CurrentLocation, Globals.DataRange))
                            {
                                player.CheckItem(item);

                                if (!player.Connection.SentChatItem.Contains(item))
                                {
                                    player.Enqueue(new S.NewChatItem { Item = item });
                                    player.Connection.SentChatItem.Add(item);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < recipients.Count; i++)
                        {
                            PlayerObject player = recipients[i];
                            if (player == this) continue;

                            if (player == null || player.Info == null || player.Node == null) continue;

                            player.CheckItem(item);

                            if (!player.Connection.SentChatItem.Contains(item))
                            {
                                player.Enqueue(new S.NewChatItem { Item = item });
                                player.Connection.SentChatItem.Add(item);
                            }
                        }
                    }

                    if (!Connection.SentChatItem.Contains(item))
                    {
                        Enqueue(new S.NewChatItem { Item = item });
                        Connection.SentChatItem.Add(item);
                    }
                }
            }

            return text;
        }
    }
}
