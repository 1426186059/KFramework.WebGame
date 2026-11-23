// partial 文件：Envir 账号/公会/攻城/角色存档等数据库读写。
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
        public void LoadAccounts()
        {
            //reset ranking
            for (var i = 0; i < RankClass.Count(); i++)
            {
                if (RankClass[i] != null)
                {
                    RankClass[i].Clear();
                }
                else
                {
                    RankClass[i] = new List<RankCharacterInfo>();
                }
            }

            RankTop.Clear();

            lock (LoadLock)
            {
                if (!File.Exists(AccountPath))
                {
                    SaveAccounts();
                }

                using (var stream = File.OpenRead(AccountPath))
                using (var reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    LoadCustomVersion = reader.ReadInt32();
                    NextAccountID = reader.ReadInt32();
                    NextCharacterID = reader.ReadInt32();
                    NextUserItemID = reader.ReadUInt64();
                    if (LoadVersion > 98)
                        NextHeroID = reader.ReadInt32();

                    GuildCount = reader.ReadInt32();
                    NextGuildID = reader.ReadInt32();

                    int count;
                    if (LoadVersion > 102)
                    {
                        count = reader.ReadInt32();

                        HeroList.Clear();

                        for (var i = 0; i < count; i++)
                            HeroList.Add(new HeroInfo(reader, LoadVersion, LoadCustomVersion));
                    }

                    count = reader.ReadInt32();

                    AccountList.Clear();
                    CharacterList.Clear();

                    int TrueAccount = 0;
                    for (var i = 0; i < count; i++)
                    {
                        AccountInfo NextAccount = new AccountInfo(reader);
                        if (i > 0 && NextAccount.Characters.Count == 0)
                            continue;
                        AccountList.Add(NextAccount);
                        CharacterList.AddRange(AccountList[TrueAccount].Characters);
                        if (LoadVersion > 98 && LoadVersion < 103)
                            AccountList[TrueAccount].Characters.ForEach(character => HeroList.AddRange(character.Heroes));
                        TrueAccount++;
                    }

                    foreach (var auction in Auctions)
                    {
                        auction.SellerInfo.AccountInfo.Auctions.Remove(auction);
                    }

                    Auctions.Clear();

                    NextAuctionID = reader.ReadUInt64();

                    count = reader.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        var auction = new AuctionInfo(reader, LoadVersion, LoadCustomVersion);

                        if (!BindItem(auction.Item) || !BindCharacter(auction)) continue;

                        Auctions.AddLast(auction);
                        auction.SellerInfo.AccountInfo.Auctions.AddLast(auction);
                    }

                    NextMailID = reader.ReadUInt64();

                    if (LoadVersion <= 80)
                    {
                        count = reader.ReadInt32();
                        for (var i = 0; i < count; i++)
                        {
                            var mail = new MailInfo(reader, LoadVersion, LoadCustomVersion);

                            mail.RecipientInfo = GetCharacterInfo(mail.RecipientIndex);

                            if (mail.RecipientInfo != null)
                            {
                                mail.RecipientInfo.Mail.Add(mail); //add to players inbox
                            }
                        }
                    }

                    if (LoadVersion >= 63)
                    {
                        var logCount = reader.ReadInt32();
                        for (var i = 0; i < logCount; i++)
                        {
                            GameshopLog.Add(reader.ReadInt32(), reader.ReadInt32());
                        }

                        if (ResetGS) ClearGameshopLog();
                    }

                    if (LoadVersion >= 68)
                    {
                        var saveCount = reader.ReadInt32();
                        for (var i = 0; i < saveCount; i++)
                        {
                            var saved = new RespawnSave(reader);
                            foreach (var respawn in SavedSpawns)
                            {
                                if (respawn.Info.RespawnIndex != saved.RespawnIndex) continue;

                                respawn.NextSpawnTick = saved.NextSpawnTick;

                                if (!saved.Spawned || respawn.Info.Count * SpawnMultiplier <= respawn.Count)
                                {
                                    continue;
                                }

                                var mobcount = respawn.Info.Count * SpawnMultiplier - respawn.Count;
                                for (var j = 0; j < mobcount; j++)
                                {
                                    respawn.Spawn();
                                }
                            }
                        }
                    }
                }
            }
        }

        public void LoadGuilds()
        {
            lock (LoadLock)
            {
                var count = 0;

                GuildList.Clear();

                for (var i = 0; i < GuildCount; i++)
                {
                    GuildInfo guildInfo;
                    if (!File.Exists(Path.Combine(Settings.GuildPath, i + ".mgd"))) continue;

                    using (var stream = File.OpenRead(Path.Combine(Settings.GuildPath, i + ".mgd")))
                    {
                        using var reader = new BinaryReader(stream);
                        guildInfo = new GuildInfo(reader);
                    }

                    GuildList.Add(guildInfo);

                    new GuildObject(guildInfo);

                    count++;
                }

                if (count != GuildCount) GuildCount = count;
            }
        }

        public void LoadConquests()
        {
            lock (LoadLock)
            {
                Conquests.Clear();
                ConquestList.Clear();

                for (var i = 0; i < ConquestInfoList.Count; i++)
                {
                    ConquestObject newConquest;
                    ConquestGuildInfo conquestGuildInfo;
                    var tempMap = GetMap(ConquestInfoList[i].MapIndex);

                    if (tempMap == null) continue;

                    if (File.Exists(Path.Combine(Settings.ConquestsPath, ConquestInfoList[i].Index + ".mcd")))
                    {
                        using (var stream = File.OpenRead(Path.Combine(Settings.ConquestsPath, ConquestInfoList[i].Index + ".mcd")))
                        {
                            using var reader = new BinaryReader(stream);
                            conquestGuildInfo = new ConquestGuildInfo(reader) { Info = ConquestInfoList[i] };
                        }

                        newConquest = new ConquestObject(conquestGuildInfo)
                        {
                            ConquestMap = tempMap
                        };

                        for (var k = 0; k < Guilds.Count; k++)
                        {
                            if (conquestGuildInfo.Owner != Guilds[k].Guildindex) continue;
                            newConquest.Guild = Guilds[k];
                            Guilds[k].Conquest = newConquest;
                        }
                    }
                    else
                    {
                        conquestGuildInfo = new ConquestGuildInfo { Info = ConquestInfoList[i], NeedSave = true };
                        newConquest = new ConquestObject(conquestGuildInfo)
                        {
                            ConquestMap = tempMap
                        };
                    }

                    ConquestList.Add(conquestGuildInfo);
                    Conquests.Add(newConquest);
                    tempMap.Conquest.Add(newConquest);

                    newConquest.Bind();
                }
            }
        }

        private void LoadGTInfo()
        {
            foreach (var gt in GTMapList)
            {
                var Guild = GuildList.FirstOrDefault(x => x.GTIndex == gt.Index);
                if (Guild != null)
                {
                    gt.Owner = Guild.Name;
                    if (Guild.Ranks.Count > 0 && Guild.Ranks[0] != null && Guild.Ranks[0].Members.Count > 0 && Guild.Ranks[0].Members[0] != null)
                        gt.Leader = Guild.Ranks[0].Members[0].Name;
                    gt.Price = 0;
                    gt.Days = (Now - Guild.GTRent).Days;
                }
            }
        }

        public void LoadDisabledChars()
        {
            DisabledCharNames.Clear();

            var path = Path.Combine(Settings.EnvirPath, "DisabledChars.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    DisabledCharNames.Add(lines[i].ToUpper());
                }
            }
        }

        public void LoadLineMessages()
        {
            LineMessages.Clear();

            var path = Path.Combine(Settings.EnvirPath, "LineMessage.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    LineMessages.Add(lines[i]);
                }
            }
        }

        private bool BindCharacter(AuctionInfo auction)
        {
            bool bound = false;

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (CharacterList[i].Index == auction.SellerIndex)
                {
                    auction.SellerInfo = CharacterList[i];
                    bound = true;
                }

                else if (CharacterList[i].Index == auction.CurrentBuyerIndex)
                {
                    auction.CurrentBuyerInfo = CharacterList[i];
                    bound = true;
                }
            }

            return bound;
        }
    }
}
