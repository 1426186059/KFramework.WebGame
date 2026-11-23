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
        public void ProcessNewDay()
        {
            foreach (var c in CharacterList)
            {
                ClearDailyQuests(c);

                c.NewDay = true;

                c.Player?.CallDefaultNPC(DefaultNPCType.Daily);
            }
        }

        private void ProcessRentedItems()
        {
            foreach (var characterInfo in CharacterList)
            {
                if (characterInfo.RentedItems.Count <= 0)
                {
                    continue;
                }

                foreach (var rentedItemInfo in characterInfo.RentedItems)
                {
                    if (rentedItemInfo.ItemReturnDate >= Now)
                        continue;

                    var rentingPlayer = GetCharacterInfo(rentedItemInfo.RentingPlayerName);

                    for (var i = 0; i < rentingPlayer.Inventory.Length; i++)
                    {
                        if (rentedItemInfo.ItemId != rentingPlayer?.Inventory[i]?.UniqueID)
                        {
                            continue;
                        }

                        var item = rentingPlayer.Inventory[i];

                        if (item?.RentalInformation == null)
                        {
                            continue;
                        }

                        if (Now <= item.RentalInformation.ExpiryDate)
                        {
                            continue;
                        }

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer, false);
                        rentingPlayer.Inventory[i] = null;
                        rentingPlayer.HasRentedItem = false;

                        if (rentingPlayer.Player == null)
                        {
                            continue;
                        }

                        rentingPlayer.Player.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemExpiredFromInventory), item.Info.FriendlyName), ChatType.System);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }

                    for (var i = 0; i < rentingPlayer.Equipment.Length; i++)
                    {
                        var item = rentingPlayer.Equipment[i];

                        if (item?.RentalInformation == null)
                        {
                            continue;
                        }

                        if (Now <= item.RentalInformation.ExpiryDate)
                        {
                            continue;
                        }

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer, false);
                        rentingPlayer.Equipment[i] = null;
                        rentingPlayer.HasRentedItem = false;

                        if (rentingPlayer.Player == null)
                        {
                            continue;
                        }

                        rentingPlayer.Player.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemExpiredInventory), item.Info.FriendlyName), ChatType.System);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }
                }
            }

            foreach (var characterInfo in CharacterList)
            {
                if (characterInfo.RentedItemsToRemove.Count <= 0)
                {
                    continue;
                }

                foreach (var rentalInformationToRemove in characterInfo.RentedItemsToRemove)
                {
                    characterInfo.RentedItems.Remove(rentalInformationToRemove);
                }

                characterInfo.RentedItemsToRemove.Clear();
            }
        }

        public bool ReturnRentalItem(UserItem rentedItem, string ownerName, CharacterInfo rentingCharacterInfo, bool removeNow = true)
        {
            if (rentedItem.RentalInformation == null)
            {
                return false;
            }

            var owner = GetCharacterInfo(ownerName);
            var returnItems = new List<UserItem>();

            foreach (var rentalInformation in owner.RentedItems)
            {
                if (rentalInformation.ItemId == rentedItem.UniqueID)
                {
                    owner.RentedItemsToRemove.Add(rentalInformation);
                }
            }

            rentedItem.RentalInformation.BindingFlags = BindMode.None;
            rentedItem.RentalInformation.RentalLocked = true;
            rentedItem.RentalInformation.ExpiryDate = rentedItem.RentalInformation.ExpiryDate.AddDays(1);

            returnItems.Add(rentedItem);

            var mail = new MailInfo(owner.Index, true)
            {
                Sender = rentingCharacterInfo.Name,
                Message = rentedItem.Info.FriendlyName,
                Items = returnItems
            };

            mail.Send();

            if (removeNow)
            {
                foreach (var rentalInformationToRemove in owner.RentedItemsToRemove)
                {
                    owner.RentedItems.Remove(rentalInformationToRemove);
                }

                owner.RentedItemsToRemove.Clear();
            }

            return true;
        }

        public GuildBuffInfo FindGuildBuffInfo(int Id)
        {
            for (var i = 0; i < Settings.Guild_BuffList.Count; i++)
            {
                if (Settings.Guild_BuffList[i].Id == Id)
                {
                    return Settings.Guild_BuffList[i];
                }
            }

            return null;
        }

        public void ClearGameshopLog()
        {
            Main.GameshopLog.Clear();

            for (var i = 0; i < AccountList.Count; i++)
            {
                for (var f = 0; f < AccountList[i].Characters.Count; f++)
                {
                    AccountList[i].Characters[f].GSpurchases.Clear();
                }
            }

            ResetGS = false;
            MessageQueue.Enqueue("Gameshop Purchase Logs Cleared.");
        }

        public void Inspect(MirConnection con, uint id)
        {
            if (ObjectID == id) return;

            PlayerObject player = Players.SingleOrDefault(x => x.ObjectID == id || x.Pets.Count(y => y.ObjectID == id && y is HumanWizard) > 0);

            if (player == null) return;
            Inspect(con, player.Info.Index);
        }

        public void Inspect(MirConnection con, int id)
        {
            if (ObjectID == id) return;

            CharacterInfo player = GetCharacterInfo(id);
            if (player == null) return;

            CharacterInfo Lover = null;
            string loverName = "";

            if (player.Married != 0) Lover = GetCharacterInfo(player.Married);

            if (Lover != null)
            {
                loverName = Lover.Name;
            }

            for (int i = 0; i < player.Equipment.Length; i++)
            {
                UserItem u = player.Equipment[i];
                if (u == null) continue;

                con.CheckItem(u);
            }

            string guildname = "";
            string guildrank = "";
            GuildObject guild = null;
            GuildRank guildRank = null;
            if (player.GuildIndex != -1)
            {
                guild = GetGuild(player.GuildIndex);
                if (guild != null)
                {
                    guildRank = guild.FindRank(player.Name);
                    if (guildRank == null)
                    {
                        guild = null;
                    }
                    else
                    {
                        guildname = guild.Name;
                        guildrank = guildRank.Name;
                    }
                }
            }

            con.Enqueue(new S.PlayerInspect
            {
                Name = player.Name,
                Equipment = player.Equipment,
                GuildName = guildname,
                GuildRank = guildrank,
                Hair = player.Hair,
                Gender = player.Gender,
                Class = player.Class,
                Level = player.Level,
                LoverName = loverName,
                AllowObserve = player.AllowObserve && Settings.AllowObserve
            });
        }

        public void InspectHero(MirConnection con, int id)
        {
            if (ObjectID == id)
            {
                return;
            }

            HeroObject heroObject = Heroes.SingleOrDefault(h => h.ObjectID == id);

            if (heroObject == null)
            {
                return;
            }

            HeroInfo heroInfo = GetHeroInfo(heroObject.Info.Index);

            if (heroInfo == null)
            {
                return;
            }

            for (int i = 0; i < heroInfo.Equipment.Length; i++)
            {
                UserItem u = heroInfo.Equipment[i];

                if (u == null)
                {
                    continue;
                }

                con.CheckItem(u);
            }

            var ownerName = heroObject.Owner.Name;

            con.Enqueue(new S.PlayerInspect
            {
                Name = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerHero), ownerName),
                Equipment = heroInfo.Equipment,
                GuildName = String.Empty,
                GuildRank = String.Empty,
                Hair = heroInfo.Hair,
                Gender = heroInfo.Gender,
                Class = heroInfo.Class,
                Level = heroInfo.Level,
                LoverName = String.Empty,
                AllowObserve = false,
                IsHero = true
            });
        }

        public void Observe(MirConnection con, string Name)
        {
            var player = GetPlayer(Name);

            if (player == null) return;
            if (!player.AllowObserve || !Settings.AllowObserve) return;

            player.AddObserver(con);
        }

        public void GetRanking(MirConnection con, byte RankType, int RankIndex, bool OnlineOnly)
        {
            if (RankType > 6) return;
            List<RankCharacterInfo> listings = RankType == 0 ? RankTop : RankClass[RankType - 1];

            if (RankIndex >= listings.Count || RankIndex < 0) return;

            S.Rankings p = new S.Rankings
            {
                RankType = RankType,
                Count = OnlineOnly ? OnlineRankingCount[RankType] : listings.Count
            };

            if (con.Player != null)
            {
                if (RankType == 0)
                    p.MyRank = con.Player.Info.Rank[0];
                else
                    p.MyRank = (byte)con.Player.Class == (RankType - 1) ? con.Player.Info.Rank[1] : 0;
            }

            int c = 0;
            for (int i = RankIndex; i < listings.Count; i++)
            {
                if (OnlineOnly && GetPlayer(listings[i].Name) == null) continue;

                if (!CheckListing(con, listings[i]))
                    p.ListingDetails.Add(listings[i]);
                p.Listings.Add(listings[i].PlayerId);
                c++;

                if (c > 19 || c >= p.Count) break;
            }

            con.Enqueue(p);
        }

        private bool CheckListing(MirConnection con, RankCharacterInfo listing)
        {
            if (!con.SentRankings.ContainsKey(listing.PlayerId))
            {
                con.SentRankings.Add(listing.PlayerId, listing.LastUpdated);
                return false;
            }

            DateTime lastUpdated = con.SentRankings[listing.PlayerId];
            if (lastUpdated != listing.LastUpdated)
            {
                con.SentRankings[listing.PlayerId] = lastUpdated;
                return false;
            }

            return true;
        }

        public int InsertRank(List<RankCharacterInfo> Ranking, RankCharacterInfo NewRank)
        {
            if (Ranking.Count == 0)
            {
                Ranking.Add(NewRank);
                return Ranking.Count;
            }

            for (var i = 0; i < Ranking.Count; i++)
            {
                //if level is lower
                if (Ranking[i].level < NewRank.level)
                {
                    Ranking.Insert(i, NewRank);
                    return i + 1;
                }

                //if exp is lower but level = same
                if (Ranking[i].level == NewRank.level && Ranking[i].Experience < NewRank.Experience)
                {
                    Ranking.Insert(i, NewRank);
                    return i + 1;
                }
            }

            Ranking.Add(NewRank);
            return Ranking.Count;
        }

        public bool TryAddRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var NewRank = new RankCharacterInfo() { Name = info.Name, Class = info.Class, Experience = info.Experience, level = info.Level, PlayerId = info.Index, info = info, LastUpdated = Now };
            var NewRankIndex = InsertRank(Ranking, NewRank);
            if (NewRankIndex == 0) return false;
            for (var i = NewRankIndex; i < Ranking.Count; i++)
            {
                SetNewRank(Ranking[i], i + 1, type);
            }
            info.Rank[type] = NewRankIndex;
            return true;
        }

        public int FindRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var startindex = info.Rank[type];
            if (startindex > 0) //if there's a previously known rank then the user can only have gone down in the ranking (or stayed the same)
            {
                for (var i = startindex - 1; i < Ranking.Count; i++)
                {
                    if (Ranking[i].Name == info.Name)
                        return i;
                }
                info.Rank[type] = 0;//set the rank to 0 to tell future searches it's not there anymore
            }
            return -1;//index can be 0
        }

        public bool UpdateRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var CurrentRank = FindRank(Ranking, info, type);
            if (CurrentRank == -1) return false;//not in ranking list atm

            var NewRank = CurrentRank;
            //next find our updated rank
            for (var i = CurrentRank - 1; i >= 0; i--)
            {
                if (Ranking[i].level > info.Level || Ranking[i].level == info.Level && Ranking[i].Experience > info.Experience) break;
                NewRank = i;
            }

            Ranking[CurrentRank].level = info.Level;
            Ranking[CurrentRank].Experience = info.Experience;
            Ranking[CurrentRank].LastUpdated = Now;

            if (NewRank < CurrentRank)
            {//if we gained any ranks
                Ranking.Insert(NewRank, Ranking[CurrentRank]);
                Ranking.RemoveAt(CurrentRank + 1);
                for (var i = NewRank + 1; i < Math.Min(Ranking.Count, CurrentRank + 1); i++)
                {
                    SetNewRank(Ranking[i], i + 1, type);
                }
            }
            info.Rank[type] = NewRank + 1;

            return true;
        }

        public void SetNewRank(RankCharacterInfo Rank, int Index, byte type)
        {
            Rank.LastUpdated = Now;
            if (!(Rank.info is CharacterInfo Player)) return;
            Player.Rank[type] = Index;
        }

        public void RemoveRank(CharacterInfo info)
        {
            List<RankCharacterInfo> Ranking;
            var Rankindex = -1;
            //first check overall top           
            Ranking = RankTop;
            Rankindex = FindRank(Ranking, info, 0);
            if (Rankindex >= 0)
            {
                Ranking.RemoveAt(Rankindex);
                for (var i = Rankindex; i < Ranking.Count(); i++)
                {
                    SetNewRank(Ranking[i], i, 0);
                }
            }

            //next class based top
            Ranking = RankTop;
            Rankindex = FindRank(Ranking, info, 1);
            if (Rankindex >= 0)
            {
                Ranking.RemoveAt(Rankindex);
                for (var i = Rankindex; i < Ranking.Count(); i++)
                {
                    SetNewRank(Ranking[i], i, 1);
                }
            }
        }

        public void CheckRankUpdate(CharacterInfo info)
        {
            List<RankCharacterInfo> Ranking;

            //first check overall top           

            Ranking = RankTop;
            if (!UpdateRank(Ranking, info, 0))
            {
                TryAddRank(Ranking, info, 0);
            }

            //now check class top

            Ranking = RankClass[(byte)info.Class];
            if (!UpdateRank(Ranking, info, 1))
            {
                TryAddRank(Ranking, info, 1);
            }
        }


    }
}
