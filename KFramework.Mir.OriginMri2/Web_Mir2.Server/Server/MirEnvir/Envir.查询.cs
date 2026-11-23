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
        public Map GetMap(int index)
        {
            return MapList.FirstOrDefault(t => t.Info.Index == index);
        }

        public Map GetMap(string name, bool strict = true)
        {
            return MapList.FirstOrDefault(t => strict ? string.Equals(t.Info.Title, name, StringComparison.CurrentCultureIgnoreCase) : t.Info.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase));
        }

        public Map GetWorldMap(string name)
        {
            return MapList.FirstOrDefault(t => t.Info.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) && t.Info.BigMap > 0);
        }

        public MapInfoTable GetMapInfo(int index)
        {
            return MapInfoList.FirstOrDefault(t => t.Index == index);
        }

        public Map GetMapByNameAndInstance(string name, int instanceValue = 0)
        {
            if (instanceValue < 0) instanceValue = 0;
            if (instanceValue > 0) instanceValue--;

            var instanceMapList = MapList.Where(t => string.Equals(t.Info.FileName, name, StringComparison.CurrentCultureIgnoreCase)).ToList();
            return instanceValue < instanceMapList.Count() ? instanceMapList[instanceValue] : null;
        }

        public MapObject GetObject(uint objectID)
        {
            return Objects.FirstOrDefault(e => e.ObjectID == objectID);
        }

        public List<MapObject> GetObjects(int map, ObjectType race)
        {
            return Objects.Where(x => x.CurrentMapIndex == map && x.Race == race).ToList();
        }

        public MonsterInfoTable GetMonsterInfo(int index)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].Index == index) return MonsterInfoList[i];

            return null;
        }

        public NPCInfoTable GetNPCInfo(int index)
        {
            for (var i = 0; i < NPCInfoList.Count; i++)
            {
                if (NPCInfoList[i].Index == index)
                    return NPCInfoList[i];
            }

            return null;
        }

        public MonsterInfoTable GetMonsterInfo(int ai, int effect = -1)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].AI == ai && (MonsterInfoList[i].Effect == effect || effect < 0)) return MonsterInfoList[i];

            return null;
        }

        public NPCObject GetNPC(string name)
        {
            return MapList.SelectMany(t1 => t1.NPCs.Where(t => t.Info.NameUI == name || t.Info.Name == name)).FirstOrDefault();
        }

        public NPCObject GetWorldMapNPC(string name)
        {
            return MapList.SelectMany(t1 => t1.NPCs.Where(t => t.Info.GameName.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) && t.Info.ShowOnBigMap)).FirstOrDefault();
        }

        public MonsterInfoTable GetMonsterInfo(int id, bool strict = false)
        {
            String monsterName = MonsterInfoList.FirstOrDefault(x => x.Index == id)?.Name;

            if (monsterName == null)
            {
                return null;
            }
            else
            {
                return (GetMonsterInfo(monsterName, strict));
            }
        }

        public MonsterInfoTable GetMonsterInfo(string name, bool Strict = false)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
            {
                var info = MonsterInfoList[i];
                if (Strict)
                {
                    if (info.Name != name) continue;
                    return info;
                }
                else
                {
                    if (string.Compare(info.Name, name, StringComparison.OrdinalIgnoreCase) != 0 &&
                        string.Compare(info.Name.Replace(" ", string.Empty), name.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase) != 0) continue;
                    return info;
                }
            }
            return null;
        }
        public PlayerObject GetPlayer(string name)
        {
            for (var i = 0; i < Players.Count; i++)
                if (string.Compare(Players[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return Players[i];

            return null;
        }
        public PlayerObject GetPlayer(uint PlayerId)
        {
            for (var i = 0; i < Players.Count; i++)
                if (Players[i].Info.Index == PlayerId)
                    return Players[i];

            return null;
        }
        public CharacterInfo GetCharacterInfo(string name)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (string.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return CharacterList[i];

            return null;
        }

        public CharacterInfo GetCharacterInfo(int index)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (CharacterList[i].Index == index)
                    return CharacterList[i];

            return null;
        }
        public HeroInfo GetHeroInfo(int index)
        {
            return HeroList.FirstOrDefault(x => x.Index == index);
        }

        public ItemInfoTable GetItemInfo(int index)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Index != index) continue;
                return info;
            }
            return null;
        }

        public ItemInfoTable GetItemInfo(string name)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (string.Compare(info.Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                return info;
            }
            return null;
        }

        public ItemInfoTable GetBook(short Skill)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Type != ItemType.Book || info.Shape != Skill) continue;
                return info;
            }
            return null;
        }

        public BuffInfo GetBuffInfo(BuffType type)
        {
            for (int i = 0; i < BuffInfoList.Count; i++)
            {
                var info = BuffInfoList[i];
                if (info.Type != type) continue;

                return info;
            }

            throw new NotImplementedException($"{type} has not been implemented.");
        }

        public void MessageAccount(AccountInfo account, string message, ChatType type)
        {
            if (account?.Characters == null) return;

            for (var i = 0; i < account.Characters.Count; i++)
            {
                if (account.Characters[i].Player == null) continue;
                account.Characters[i].Player.ReceiveChat(message, type);
                return;
            }
        }


        public void MailCharacter(CharacterInfo info, UserItem item = null, uint gold = 0, int reason = 0, string customMessage = null)
        {
            string sender = "Bichon Administrator";

            string message = "You have been mailed due to the following reason:\r\n\r\n";

            switch (reason)
            {
                case 1:
                    message += "Could not return item to bag after trade.";
                    break;
                case 99:
                    message += "Code didn't correctly handle checking inventory space.";
                    break;
                default:
                    message += customMessage ?? "No reason provided.";
                    break;
            }

            MailInfo mail = new MailInfo(info.Index)
            {
                Sender = sender,
                Message = message,
                Gold = gold
            };

            if (item != null)
            {
                mail.Items.Add(item);
            }

            mail.Send();
        }

        public GuildObject GetGuild(string name)
        {
            for (var i = 0; i < Guilds.Count; i++)
            {
                if (string.Compare(Guilds[i].Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;

                return Guilds[i];
            }

            return null;
        }
        public GuildObject GetGuild(int index)
        {
            for (var i = 0; i < Guilds.Count; i++)
            {
                if (Guilds[i].Guildindex == index)
                {
                    return Guilds[i];
                }
            }

            return null;
        }

    }
}
