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
        #region Trading

        public void DepositTradeItem(int from, int to)
        {
            S.DepositTradeItem p = new S.DepositTradeItem { From = from, To = to, Success = false };

            if (from < 0 || from >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Info.Trade.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem temp = Info.Inventory[from];

            if (temp == null)
            {
                Enqueue(p);
                return;
            }

            if (temp.Info.Bind.HasFlag(BindMode.DontTrade))
            {
                Enqueue(p);
                return;
            }

            if (temp.RentalInformation != null && temp.RentalInformation.BindingFlags.HasFlag(BindMode.DontTrade))
            {
                Enqueue(p);
                return;
            }

            if (Info.Trade[to] == null)
            {
                Info.Trade[to] = temp;
                Info.Inventory[from] = null;
                RefreshBagWeight();
                TradeItem();

                Report.ItemMoved(temp, MirGridType.Inventory, MirGridType.Trade, from, to);

                p.Success = true;
                Enqueue(p);
                return;
            }
            Enqueue(p);

        }
        public void RetrieveTradeItem(int from, int to)
        {
            S.RetrieveTradeItem p = new S.RetrieveTradeItem { From = from, To = to, Success = false };

            if (from < 0 || from >= Info.Trade.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem temp = Info.Trade[from];

            if (temp == null)
            {
                Enqueue(p);
                return;
            }

            if (Info.Inventory[to] == null)
            {
                Info.Inventory[to] = temp;
                Info.Trade[from] = null;

                p.Success = true;
                RefreshBagWeight();
                TradeItem();

                Report.ItemMoved(temp, MirGridType.Trade, MirGridType.Inventory, from, to);
            }

            Enqueue(p);
        }



        public void TradeRequest()
        {
            if (Envir.Time < NextTradeTime) return;
            NextTradeTime = Envir.Time + Settings.TradeDelay;

            if (TradePartner != null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.AlreadyTrading), ChatType.System);
                return;
            }

            Point target = Functions.PointMove(CurrentLocation, Direction, 1);

            if (!CurrentMap.ValidPoint(target)) return;
            Cell cell = CurrentMap.GetCell(target);
            PlayerObject player = null;

            if (cell.Objects == null || cell.Objects.Count == 0)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.FaceToTrade), ChatType.System);
                return;
            }

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (ob.Race != ObjectType.Player) continue;

                player = Envir.GetPlayer(ob.Name);
            }

            if (player == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.FaceToTrade), ChatType.System);
                return;
            }

            if (player != null)
            {
                if (!Functions.FacingEachOther(Direction, CurrentLocation, player.Direction, player.CurrentLocation))
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.FaceToTrade), ChatType.System);
                    return;
                }

                if (player == this)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotTradeWithSelf), ChatType.System);
                    return;
                }

                if (player.Dead || Dead)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotTradeWhenDead), ChatType.System);
                    return;
                }

                if (player.TradeInvitation != null)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerAlreadyHasTradeInvitation), player.Info.Name), ChatType.System);
                    return;
                }

                if (!player.AllowTrade)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerNotAllowingTrade), player.Info.Name), ChatType.System);
                    return;
                }

                if (!Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) || player.CurrentMap != CurrentMap)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerNotInTradeRange), player.Info.Name), ChatType.System);
                    return;
                }

                if (player.TradePartner != null)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerAlreadyTrading), player.Info.Name), ChatType.System);
                    return;
                }

                player.TradeInvitation = this;
                player.Enqueue(new S.TradeRequest { Name = Info.Name });
            }
        }
        public void TradeReply(bool accept)
        {
            if (TradeInvitation == null || TradeInvitation.Info == null)
            {
                TradeInvitation = null;
                return;
            }

            if (!accept)
            {
                TradeInvitation.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerRefusedTrade), Info.Name), ChatType.System);
                TradeInvitation = null;
                return;
            }

            if (TradePartner != null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.AlreadyTrading), ChatType.System);
                TradeInvitation = null;
                return;
            }

            if (TradeInvitation.TradePartner != null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerAlreadyTrading), TradeInvitation.Info.Name), ChatType.System);
                TradeInvitation = null;
                return;
            }

            TradePartner = TradeInvitation;
            TradeInvitation.TradePartner = this;
            TradeInvitation = null;

            Enqueue(new S.TradeAccept { Name = TradePartner.Info.Name });
            TradePartner.Enqueue(new S.TradeAccept { Name = Info.Name });
        }
        public void TradeGold(uint amount)
        {
            TradeUnlock();

            if (TradePartner == null) return;

            if (amount < 1 || Account.Gold < amount)
            {
                return;
            }

            TradeGoldAmount += amount;
            Account.Gold -= amount;

            Enqueue(new S.LoseGold { Gold = amount });
            TradePartner.Enqueue(new S.TradeGold { Amount = TradeGoldAmount });
        }
        public void TradeItem()
        {
            TradeUnlock();

            if (TradePartner == null) return;

            for (int i = 0; i < Info.Trade.Length; i++)
            {
                UserItem u = Info.Trade[i];
                if (u == null) continue;

                //TradePartner.CheckItemInfo(u.Info);
                TradePartner.CheckItem(u);
            }

            TradePartner.Enqueue(new S.TradeItem { TradeItems = Info.Trade });
        }

        public void TradeUnlock()
        {
            TradeLocked = false;

            if (TradePartner != null)
            {
                TradePartner.TradeLocked = false;
            }
        }

        public void TradeConfirm(bool confirm)
        {
            if (!confirm)
            {
                TradeLocked = false;
                return;
            }

            if (TradePartner == null)
            {
                TradeCancel();
                return;
            }

            if (!Functions.InRange(TradePartner.CurrentLocation, CurrentLocation, Globals.DataRange) || TradePartner.CurrentMap != CurrentMap ||
                !Functions.FacingEachOther(Direction, CurrentLocation, TradePartner.Direction, TradePartner.CurrentLocation))
            {
                TradeCancel();
                return;
            }

            TradeLocked = true;

            if (TradeLocked && !TradePartner.TradeLocked)
            {
                TradePartner.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerWaitingConfirmTrade), Info.Name), ChatType.System);
            }

            if (!TradeLocked || !TradePartner.TradeLocked) return;

            PlayerObject[] TradePair = new PlayerObject[2] { TradePartner, this };

            bool CanTrade = true;
            UserItem u;

            //check if both people can accept the others items
            for (int p = 0; p < 2; p++)
            {
                int o = p == 0 ? 1 : 0;

                if (!TradePair[o].CanGainItems(TradePair[p].Info.Trade))
                {
                    CanTrade = false;
                    TradePair[p].ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TradingPartnerCannotAcceptAllItems), ChatType.System);
                    TradePair[p].Enqueue(new S.TradeCancel { Unlock = true });

                    TradePair[o].ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.UnableAcceptAllItems), ChatType.System);
                    TradePair[o].Enqueue(new S.TradeCancel { Unlock = true });

                    return;
                }

                if (!TradePair[o].CanGainGold(TradePair[p].TradeGoldAmount))
                {
                    CanTrade = false;
                    TradePair[p].ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.PartnerCannotAcceptMoreGold), ChatType.System);
                    TradePair[p].Enqueue(new S.TradeCancel { Unlock = true });

                    TradePair[o].ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.UnableAcceptMoreGold), ChatType.System);
                    TradePair[o].Enqueue(new S.TradeCancel { Unlock = true });

                    return;
                }
            }

            //swap items
            if (CanTrade)
            {
                for (int p = 0; p < 2; p++)
                {
                    int o = p == 0 ? 1 : 0;

                    for (int i = 0; i < TradePair[p].Info.Trade.Length; i++)
                    {
                        u = TradePair[p].Info.Trade[i];

                        if (u == null) continue;

                        TradePair[o].GainItem(u);
                        TradePair[p].Info.Trade[i] = null;

                        Report.ItemMoved(u, MirGridType.Trade, MirGridType.Inventory, i, -99, GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TradeFromTo), TradePair[p].Name, TradePair[o].Name));
                    }

                    if (TradePair[p].TradeGoldAmount > 0)
                    {
                        Report.GoldChanged(TradePair[p].TradeGoldAmount, true, GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TradeFromTo), TradePair[p].Name, TradePair[o].Name));

                        TradePair[o].GainGold(TradePair[p].TradeGoldAmount);
                        TradePair[p].TradeGoldAmount = 0;
                    }

                    TradePair[p].ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TradeSuccessful), ChatType.System);
                    TradePair[p].Enqueue(new S.TradeConfirm());

                    TradePair[p].TradeLocked = false;
                    TradePair[p].TradePartner = null;
                }
            }
        }
        public void TradeCancel()
        {
            TradeUnlock();

            if (TradePartner == null)
            {
                return;
            }

            PlayerObject[] TradePair = new PlayerObject[2] { TradePartner, this };

            for (int p = 0; p < 2; p++)
            {
                if (TradePair[p] != null)
                {
                    for (int t = 0; t < TradePair[p].Info.Trade.Length; t++)
                    {
                        UserItem temp = TradePair[p].Info.Trade[t];

                        if (temp == null) continue;

                        if (FreeSpace(TradePair[p].Info.Inventory) < 1)
                        {
                            TradePair[p].GainItemMail(temp, 1);
                            Report.ItemMailed(temp, temp.Count, 1);

                            TradePair[p].Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });
                            TradePair[p].Info.Trade[t] = null;
                            continue;
                        }

                        for (int i = 0; i < TradePair[p].Info.Inventory.Length; i++)
                        {
                            if (TradePair[p].Info.Inventory[i] != null) continue;

                            //Put item back in inventory
                            if (TradePair[p].CanGainItem(temp))
                            {
                                TradePair[p].RetrieveTradeItem(t, i);
                            }
                            else //Send item to mailbox if it can no longer be stored
                            {
                                TradePair[p].GainItemMail(temp, 1);
                                Report.ItemMailed(temp, temp.Count, 1);

                                TradePair[p].Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });
                            }

                            TradePair[p].Info.Trade[t] = null;

                            break;
                        }
                    }

                    //Put back deposited gold
                    if (TradePair[p].TradeGoldAmount > 0)
                    {
                        Report.GoldChanged(TradePair[p].TradeGoldAmount, false);

                        TradePair[p].GainGold(TradePair[p].TradeGoldAmount);
                        TradePair[p].TradeGoldAmount = 0;
                    }

                    TradePair[p].TradeLocked = false;
                    TradePair[p].TradePartner = null;

                    TradePair[p].Enqueue(new S.TradeCancel { Unlock = false });
                }
            }
        }

        #endregion        

        #region Fishing

        public void FishingCast(bool cast, bool cancel = false)
        {
            UserItem rod = Info.Equipment[(int)EquipmentSlot.Weapon];

            byte flexibilityStat = 0;
            sbyte successStat = 0;
            byte nibbleMin = 0, nibbleMax = 0;
            byte failedAddSuccessMin = 0, failedAddSuccessMax = 0;
            FishingProgressMax = Settings.FishingAttempts;//30;

            if (rod == null || !rod.Info.IsFishingRod || rod.CurrentDura == 0)
            {
                Fishing = false;
                return;
            }

            Point fishingPoint = Functions.PointMove(CurrentLocation, Direction, 3);

            if (fishingPoint.X < 0 || fishingPoint.Y < 0 || CurrentMap.Width < fishingPoint.X || CurrentMap.Height < fishingPoint.Y)
            {
                Fishing = false;
                return;
            }

            Cell fishingCell = CurrentMap.Cells[fishingPoint.X, fishingPoint.Y];

            if (fishingCell.FishingAttribute < 0)
            {
                Fishing = false;
                return;
            }

            flexibilityStat = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, flexibilityStat + rod.Info.Stats[Stat.CriticalRate])));
            successStat = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, successStat + rod.Info.Stats[Stat.MaxAC])));

            if (cast)
            {
                DamageItem(rod, 1, true);
            }

            UserItem hook = rod.Slots[(int)FishingSlot.Hook];

            if (hook == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NeedHook), ChatType.System);
                return;
            }
            else
            {
                DamagedFishingItem(FishingSlot.Hook, 1);
            }

            foreach (UserItem temp in rod.Slots)
            {
                if (temp == null) continue;

                ItemInfoTable realItem = Functions.GetRealItem(temp.Info, Info.Level, Info.Class, Envir.ItemInfoList);

                switch (realItem.Type)
                {
                    case ItemType.Hook:
                        {
                            flexibilityStat = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, flexibilityStat + temp.AddedStats[Stat.CriticalRate] + realItem.Stats[Stat.CriticalRate])));
                        }
                        break;
                    case ItemType.Float:
                        {
                            nibbleMin = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, nibbleMin + realItem.Stats[Stat.MinAC])));
                            nibbleMax = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, nibbleMax + realItem.Stats[Stat.MaxAC])));
                        }
                        break;
                    case ItemType.Bait:
                        {
                            successStat = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, successStat + realItem.Stats[Stat.MaxAC])));
                        }
                        break;
                    case ItemType.Finder:
                        {
                            failedAddSuccessMin = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, failedAddSuccessMin + realItem.Stats[Stat.MinAC])));
                            failedAddSuccessMax = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, failedAddSuccessMax + realItem.Stats[Stat.MaxAC])));
                        }
                        break;
                    case ItemType.Reel:
                        {
                            FishingAutoReelChance = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, FishingAutoReelChance + realItem.Stats[Stat.MaxMAC])));
                            successStat = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, successStat + realItem.Stats[Stat.MaxAC])));
                        }
                        break;
                    default:
                        break;
                }
            }

            FishingNibbleChance = 5 + Envir.Random.Next(nibbleMin, nibbleMax);

            if (cast) FishingChance = Settings.FishingSuccessStart + (int)successStat + (FishingChanceCounter != 0 ? Envir.Random.Next(failedAddSuccessMin, failedAddSuccessMax) : 0) + (FishingChanceCounter * Settings.FishingSuccessMultiplier); //10 //10
            if (FishingChanceCounter != 0) DamagedFishingItem(FishingSlot.Finder, 1);
            FishingChance += Stats[Stat.FishRatePercent];

            FishingChance = Math.Min(100, Math.Max(0, FishingChance));
            FishingNibbleChance = Math.Min(100, Math.Max(0, FishingNibbleChance));
            FishingAutoReelChance = Math.Min(100, Math.Max(0, FishingAutoReelChance));

            FishingTime = Envir.Time + FishingCastDelay + Settings.FishingDelay;

            if (cast)
            {
                if (Fishing) return;

                _fishCounter = 0;
                FishFound = false;

                UserItem item = GetBait(1);

                if (item == null)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouNeedBait), ChatType.System);
                    return;
                }

                ConsumeItem(item, 1);
                Fishing = true;
            }
            else
            {
                if (!Fishing)
                {
                    Enqueue(GetFishInfo());
                    return;
                }

                Fishing = false;

                if (FishingProgress > 99)
                {
                    FishingChanceCounter++;
                }

                if (FishFound)
                {
                    int getChance = FishingChance + Envir.Random.Next(10, 24) + (FishingProgress > 50 ? flexibilityStat / 2 : 0);
                    getChance = Math.Min(100, Math.Max(0, getChance));

                    if (Envir.Random.Next(0, 100) <= getChance)
                    {
                        FishingChanceCounter = 0;

                        UserItem dropItem = null;
                        UserItem reel = rod.Slots[(int)FishingSlot.Reel];

                        foreach (DropInfo drop in Envir.FishingDrops.Where(x => x.Type == fishingCell.FishingAttribute))
                        {
                            var reward = drop.AttemptDrop(EXPOwner?.Stats[Stat.ItemDropRatePercent] ?? 0, EXPOwner?.Stats[Stat.GoldDropRatePercent] ?? 0);

                            if (reward != null)
                            {
                                foreach (var dropitems in reward.Items)
                                {
                                    dropItem = Envir.CreateDropItem(drop.Item);
                                    break;
                                }
                            }
                        }

                        if (dropItem == null)
                        {
                            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.FishGotAway), ChatType.System);
                        }
                        else if (FreeSpace(Info.Inventory) < 1)
                        {
                            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NoBagSpace), ChatType.System);
                            cancel = true;
                        }
                        else
                        {
                            GainItem(dropItem);
                            Report.ItemChanged(dropItem, dropItem.Count, 2);
                        }

                        if (Envir.Random.Next(100 - Settings.FishingMobSpawnChance) == 0)
                        {
                            MonsterObject mob = MonsterObject.GetMonster(Envir.GetMonsterInfo(Settings.FishingMonster));

                            if (mob == null) return;

                            mob.Spawn(CurrentMap, Back);
                        }

                        DamagedFishingItem(FishingSlot.Reel, 1);

                        if (!FishingAutocast || (FishingAutocast && reel.CurrentDura == 0))
                        {
                            cancel = true;
                        }
                    }
                    else
                    {
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.FishGotAway), ChatType.System);
                    }
                }

                FishFound = false;
                FishFirstFound = false;
            }

            Enqueue(GetFishInfo());
            Broadcast(GetFishInfo());

            if (FishingAutocast && !cast && !cancel)
            {
                FishingTime = Envir.Time + (FishingCastDelay * 2);
                FishingFoundTime = Envir.Time;
                FishingAutoReelChance = 0;
                FishingNibbleChance = 0;
                FishFirstFound = false;

                FishingCast(true);
            }
        }
        public void FishingChangeAutocast(bool autoCast)
        {
            UserItem rod = Info.Equipment[(int)EquipmentSlot.Weapon];

            if (rod == null || !rod.Info.IsFishingRod) return;

            UserItem reel = rod.Slots[(int)FishingSlot.Reel];

            if (reel == null)
            {
                FishingAutocast = false;
                return;
            }

            FishingAutocast = autoCast;
        }
        public void UpdateFish()
        {
            if (FishFound != true && FishFirstFound != true)
            {
                FishFound = Envir.Random.Next(0, 100) <= FishingNibbleChance;
                FishingFoundTime = FishFound ? Envir.Time + 3000 : Envir.Time;

                if (FishFound)
                {
                    FishFirstFound = true;
                    DamagedFishingItem(FishingSlot.Float, 1);
                }
            }
            else
            {
                if (FishingAutoReelChance != 0 && Envir.Random.Next(0, 100) <= FishingAutoReelChance)
                {
                    FishingCast(false);
                }
            }

            if (FishingFoundTime < Envir.Time)
                FishFound = false;

            FishingTime = Envir.Time + FishingDelay;

            Enqueue(GetFishInfo());

            if (FishingProgress > 100)
            {
                FishingCast(false);
            }
        }
        Packet GetFishInfo()
        {
            FishingProgress = _fishCounter > 0 ? (int)(((decimal)_fishCounter / FishingProgressMax) * 100) : 0;

            return new S.FishingUpdate
            {
                ObjectID = ObjectID,
                Fishing = Fishing,
                ProgressPercent = FishingProgress,
                FishingPoint = Functions.PointMove(CurrentLocation, Direction, 3),
                ChancePercent = FishingChance,
                FoundFish = FishFound
            };
        }

        #endregion
    }
}
