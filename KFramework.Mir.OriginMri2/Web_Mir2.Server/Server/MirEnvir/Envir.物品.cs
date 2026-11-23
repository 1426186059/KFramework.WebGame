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
        public void CreateAccountInfo()
        {
            AccountList.Add(new AccountInfo { Index = ++NextAccountID });
        }
        public void CreateMapInfo()
        {
            MapInfoList.Add(new MapInfoTable { Index = ++MapIndex });
        }
        public void CreateItemInfo(ItemType type = ItemType.Nothing)
        {
            ItemInfoList.Add(new ItemInfoTable { Index = ++ItemIndex, Type = type, RandomStatsId = 255 });
        }
        public void CreateMonsterInfo()
        {
            MonsterInfoList.Add(new MonsterInfoTable { Index = ++MonsterIndex });
        }
        public void CreateNPCInfo()
        {
            NPCInfoList.Add(new NPCInfoTable { Index = ++NPCIndex });
        }
        public void CreateQuestInfo()
        {
            QuestInfoList.Add(new QuestInfoTable { Index = ++QuestIndex });
        }

        public void AddToGameShop(ItemInfoTable Info)
        {
            GameShopList.Add(new GameShopItemTable
            {
                GIndex = ++GameshopIndex,
                GoldPrice = (uint)(1000 * Settings.CredxGold),
                CreditPrice = 1000,
                ItemIndex = Info.Index,
                Info = Info,
                Date = Now,
                Class = "All",
                Category = Info.Type.ToString()
            });
        }

        public void Remove(MapInfoTable info)
        {
            MapInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(ItemInfoTable info)
        {
            ItemInfoList.Remove(info);
        }
        public void Remove(MonsterInfoTable info)
        {
            MonsterInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(NPCInfoTable info)
        {
            NPCInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(QuestInfoTable info)
        {
            QuestInfoList.Remove(info);
            //Desync all objects\
        }

        public void Remove(GameShopItemTable info)
        {
            GameShopList.Remove(info);

            if (GameShopList.Count == 0)
            {
                GameshopIndex = 0;
            }

            //Desync all objects\
        }

        public UserItem CreateFreshItem(ItemInfoTable info)
        {
            var item = new UserItem(info)
            {
                UniqueID = ++NextUserItemID,
                CurrentDura = info.Durability,
                MaxDura = info.Durability
            };

            UpdateItemExpiry(item);

            return item;
        }
        public UserItem CreateDropItem(int index)
        {
            return CreateDropItem(GetItemInfo(index));
        }
        public UserItem CreateDropItem(ItemInfoTable info)
        {
            if (info == null) return null;

            var item = new UserItem(info)
            {
                UniqueID = ++NextUserItemID,
                MaxDura = info.Durability,
                CurrentDura = (ushort)Math.Min(info.Durability, Random.Next(info.Durability) + 1000)
            };

            UpgradeItem(item);

            UpdateItemExpiry(item);

            if (!info.NeedIdentify) item.Identified = true;
            return item;
        }

        public UserItem CreateShopItem(ItemInfoTable info, ulong id)
        {
            if (info == null) return null;

            var item = new UserItem(info)
            {
                UniqueID = id,
                CurrentDura = info.Durability,
                MaxDura = info.Durability,
                IsShopItem = true,
            };

            return item;
        }

        public void UpdateItemExpiry(UserItem item)
        {
            var expiryInfo = new ExpireInfo();

            var r = new Regex(@"\[(.*?)\]");
            var expiryMatch = r.Match(item.Info.Name);

            if (expiryMatch.Success)
            {
                var parameter = expiryMatch.Groups[1].Captures[0].Value;

                var numAlpha = new Regex("(?<Numeric>[0-9]*)(?<Alpha>[a-zA-Z]*)");
                var match = numAlpha.Match(parameter);

                var alpha = match.Groups["Alpha"].Value;
                var num = 0;

                int.TryParse(match.Groups["Numeric"].Value, out num);

                switch (alpha)
                {
                    case "m":
                        expiryInfo.ExpiryDate = Now.AddMinutes(num);
                        break;
                    case "h":
                        expiryInfo.ExpiryDate = Now.AddHours(num);
                        break;
                    case "d":
                        expiryInfo.ExpiryDate = Now.AddDays(num);
                        break;
                    case "M":
                        expiryInfo.ExpiryDate = Now.AddMonths(num);
                        break;
                    case "y":
                        expiryInfo.ExpiryDate = Now.AddYears(num);
                        break;
                    default:
                        expiryInfo.ExpiryDate = DateTime.MaxValue;
                        break;
                }

                item.ExpireInfo = expiryInfo;
            }
        }

        public void UpgradeItem(UserItem item)
        {
            if (item.Info.RandomStats == null) return;
            var stat = item.Info.RandomStats;
            if (stat.MaxDuraChance > 0 && Random.Next(stat.MaxDuraChance) == 0)
            {
                var dura = RandomomRange(stat.MaxDuraMaxStat, stat.MaxDuraStatChance);
                item.MaxDura = (ushort)Math.Min(ushort.MaxValue, item.MaxDura + dura * 1000);
                item.CurrentDura = (ushort)Math.Min(ushort.MaxValue, item.CurrentDura + dura * 1000);
            }

            if (stat.MaxAcChance > 0 && Random.Next(stat.MaxAcChance) == 0) item.AddedStats[Stat.MaxAC] = (byte)(RandomomRange(stat.MaxAcMaxStat - 1, stat.MaxAcStatChance) + 1);
            if (stat.MaxMacChance > 0 && Random.Next(stat.MaxMacChance) == 0) item.AddedStats[Stat.MaxMAC] = (byte)(RandomomRange(stat.MaxMacMaxStat - 1, stat.MaxMacStatChance) + 1);
            if (stat.MaxDcChance > 0 && Random.Next(stat.MaxDcChance) == 0) item.AddedStats[Stat.MaxDC] = (byte)(RandomomRange(stat.MaxDcMaxStat - 1, stat.MaxDcStatChance) + 1);
            if (stat.MaxMcChance > 0 && Random.Next(stat.MaxMcChance) == 0) item.AddedStats[Stat.MaxMC] = (byte)(RandomomRange(stat.MaxMcMaxStat - 1, stat.MaxMcStatChance) + 1);
            if (stat.MaxScChance > 0 && Random.Next(stat.MaxScChance) == 0) item.AddedStats[Stat.MaxSC] = (byte)(RandomomRange(stat.MaxScMaxStat - 1, stat.MaxScStatChance) + 1);
            if (stat.AccuracyChance > 0 && Random.Next(stat.AccuracyChance) == 0) item.AddedStats[Stat.Accuracy] = (byte)(RandomomRange(stat.AccuracyMaxStat - 1, stat.AccuracyStatChance) + 1);
            if (stat.AgilityChance > 0 && Random.Next(stat.AgilityChance) == 0) item.AddedStats[Stat.Agility] = (byte)(RandomomRange(stat.AgilityMaxStat - 1, stat.AgilityStatChance) + 1);
            if (stat.HpChance > 0 && Random.Next(stat.HpChance) == 0) item.AddedStats[Stat.HP] = (byte)(RandomomRange(stat.HpMaxStat - 1, stat.HpStatChance) + 1);
            if (stat.MpChance > 0 && Random.Next(stat.MpChance) == 0) item.AddedStats[Stat.MP] = (byte)(RandomomRange(stat.MpMaxStat - 1, stat.MpStatChance) + 1);
            if (stat.StrongChance > 0 && Random.Next(stat.StrongChance) == 0) item.AddedStats[Stat.Strong] = (byte)(RandomomRange(stat.StrongMaxStat - 1, stat.StrongStatChance) + 1);
            if (stat.MagicResistChance > 0 && Random.Next(stat.MagicResistChance) == 0) item.AddedStats[Stat.MagicResist] = (byte)(RandomomRange(stat.MagicResistMaxStat - 1, stat.MagicResistStatChance) + 1);
            if (stat.PoisonResistChance > 0 && Random.Next(stat.PoisonResistChance) == 0) item.AddedStats[Stat.PoisonResist] = (byte)(RandomomRange(stat.PoisonResistMaxStat - 1, stat.PoisonResistStatChance) + 1);
            if (stat.HpRecovChance > 0 && Random.Next(stat.HpRecovChance) == 0) item.AddedStats[Stat.HealthRecovery] = (byte)(RandomomRange(stat.HpRecovMaxStat - 1, stat.HpRecovStatChance) + 1);
            if (stat.MpRecovChance > 0 && Random.Next(stat.MpRecovChance) == 0) item.AddedStats[Stat.SpellRecovery] = (byte)(RandomomRange(stat.MpRecovMaxStat - 1, stat.MpRecovStatChance) + 1);
            if (stat.PoisonRecovChance > 0 && Random.Next(stat.PoisonRecovChance) == 0) item.AddedStats[Stat.PoisonRecovery] = (byte)(RandomomRange(stat.PoisonRecovMaxStat - 1, stat.PoisonRecovStatChance) + 1);
            if (stat.CriticalRateChance > 0 && Random.Next(stat.CriticalRateChance) == 0) item.AddedStats[Stat.CriticalRate] = (byte)(RandomomRange(stat.CriticalRateMaxStat - 1, stat.CriticalRateStatChance) + 1);
            if (stat.CriticalDamageChance > 0 && Random.Next(stat.CriticalDamageChance) == 0) item.AddedStats[Stat.CriticalDamage] = (byte)(RandomomRange(stat.CriticalDamageMaxStat - 1, stat.CriticalDamageStatChance) + 1);
            if (stat.FreezeChance > 0 && Random.Next(stat.FreezeChance) == 0) item.AddedStats[Stat.Freezing] = (byte)(RandomomRange(stat.FreezeMaxStat - 1, stat.FreezeStatChance) + 1);
            if (stat.PoisonAttackChance > 0 && Random.Next(stat.PoisonAttackChance) == 0) item.AddedStats[Stat.PoisonAttack] = (byte)(RandomomRange(stat.PoisonAttackMaxStat - 1, stat.PoisonAttackStatChance) + 1);
            if (stat.AttackSpeedChance > 0 && Random.Next(stat.AttackSpeedChance) == 0) item.AddedStats[Stat.AttackSpeed] = (sbyte)(RandomomRange(stat.AttackSpeedMaxStat - 1, stat.AttackSpeedStatChance) + 1);
            if (stat.LuckChance > 0 && Random.Next(stat.LuckChance) == 0) item.AddedStats[Stat.Luck] = (sbyte)(RandomomRange(stat.LuckMaxStat - 1, stat.LuckStatChance) + 1);
            if (stat.CurseChance > 0 && Random.Next(100) <= stat.CurseChance) item.Cursed = true;

            if (stat.SlotChance > 0 && Random.Next(stat.SlotChance) == 0)
            {
                var slot = (byte)(RandomomRange(stat.SlotMaxStat - 1, stat.SlotStatChance) + 1);

                if (slot > item.Info.Slots)
                {
                    item.SetSlotSize(slot);
                }
            }
        }

        public int RandomomRange(int count, int rate)
        {
            var x = 0;
            for (var i = 0; i < count; i++) if (Random.Next(rate) == 0) x++;
            return x;
        }
        public bool BindItem(UserItem item)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return BindSlotItems(item);
            }
            return false;
        }

        public bool BindGameShop(GameShopItemTable item, bool editEnvir = true)
        {
            for (var i = 0; i < Edit.ItemInfoList.Count; i++)
            {
                var info = Edit.ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return true;
            }
            return false;
        }

        public bool BindSlotItems(UserItem item)
        {
            for (var i = 0; i < item.Slots.Length; i++)
            {
                if (item.Slots[i] == null) continue;

                if (!BindItem(item.Slots[i])) return false;
            }

            return true;
        }

        public bool BindQuest(QuestProgressInfo quest)
        {
            for (var i = 0; i < QuestInfoList.Count; i++)
            {
                var info = QuestInfoList[i];
                if (info.Index != quest.Index) continue;
                quest.Info = info;
                return true;
            }
            return false;
        }

    }
}
