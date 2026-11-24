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
        #region Friends
        public void AddFriend(string name, bool blocked = false)
        {
            CharacterInfo info = Envir.GetCharacterInfo(name);

            if (info == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.PlayerDoesNotExist), ChatType.System);
                return;
            }

            if (Name == name)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotAddYourself), ChatType.System);
                return;
            }

            if (Info.Friends.Any(e => e.Index == info.Index))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.PlayerAlreadyAdded), ChatType.System);
                return;
            }

            FriendInfo friend = new FriendInfo(info, blocked);

            Info.Friends.Add(friend);

            GetFriends();
        }

        public void RemoveFriend(int index)
        {
            FriendInfo friend = Info.Friends.FirstOrDefault(e => e.Index == index);

            if (friend == null)
            {
                return;
            }

            Info.Friends.Remove(friend);

            GetFriends();
        }

        public void AddMemo(int index, string memo)
        {
            if (string.IsNullOrEmpty(memo) || memo.Length > 200) return;

            FriendInfo friend = Info.Friends.FirstOrDefault(e => e.Index == index);

            if (friend == null)
            {
                return;
            }

            friend.Memo = memo;

            GetFriends();
        }

        public void GetFriends()
        {
            List<ClientFriend> friends = new List<ClientFriend>();

            foreach (FriendInfo friend in Info.Friends)
            {
                if (friend.Info != null)
                {
                    friends.Add(friend.CreateClientFriend());
                }
            }

            Enqueue(new S.FriendUpdate { Friends = friends });
        }

        #endregion

        #region Refining

        public void DepositRefineItem(int from, int to)
        {

            S.DepositRefineItem p = new S.DepositRefineItem { From = from, To = to, Success = false };

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.RefineKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(p);
                return;
            }
            NPCObject ob = null;
            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                if (CurrentMap.NPCs[i].ObjectID != NPCObjectID) continue;
                ob = CurrentMap.NPCs[i];
                break;
            }

            if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
            {
                Enqueue(p);
                return;
            }


            if (from < 0 || from >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Info.Refine.Length)
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

            if (Info.Refine[to] == null)
            {
                Info.Refine[to] = temp;
                Info.Inventory[from] = null;
                RefreshBagWeight();

                Report.ItemMoved(temp, MirGridType.Inventory, MirGridType.Refine, from, to);

                p.Success = true;
                Enqueue(p);
                return;
            }
            Enqueue(p);

        }
        public void RetrieveRefineItem(int from, int to)
        {
            S.RetrieveRefineItem p = new S.RetrieveRefineItem { From = from, To = to, Success = false };

            if (from < 0 || from >= Info.Refine.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem temp = Info.Refine[from];

            if (temp == null)
            {
                Enqueue(p);
                return;
            }

            if (Info.Inventory[to] == null)
            {
                Info.Inventory[to] = temp;
                Info.Refine[from] = null;

                Report.ItemMoved(temp, MirGridType.Refine, MirGridType.Inventory, from, to);

                p.Success = true;
                RefreshBagWeight();
                Enqueue(p);

                return;
            }
            Enqueue(p);
        }
        public void RefineCancel()
        {
            for (int t = 0; t < Info.Refine.Length; t++)
            {
                UserItem temp = Info.Refine[t];

                if (temp == null) continue;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    if (Info.Inventory[i] != null) continue;

                    //Put item back in inventory
                    if (CanGainItem(temp))
                    {
                        RetrieveRefineItem(t, i);
                    }
                    else //Send item via mail if it can no longer be stored
                    {
                        Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });

                        MailInfo mail = new MailInfo(Info.Index)
                        {
                            MailID = ++Envir.NextMailID,
                            Sender = "Refiner",
                            Message = GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.RefiningCancelledItemNotReturned),
                            Items = new List<UserItem> { temp },
                        };

                        mail.Send();
                    }

                    Info.Refine[t] = null;

                    break;
                }
            }
        }
        public void RefineItem(ulong uniqueID)
        {
            Enqueue(new S.RepairItem { UniqueID = uniqueID }); //CHECK THIS.

            if (Dead) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCScript.RefineKey, StringComparison.CurrentCultureIgnoreCase))) return;

            int index = -1;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] == null || Info.Inventory[i].UniqueID != uniqueID) continue;
                index = i;
                break;
            }

            if (index == -1) return;

            if (Info.Inventory[index].RefineAdded != 0)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CheckBeforeRefine), Info.Inventory[index].FriendlyName), ChatType.System);
                return;
            }

            if ((Info.Inventory[index].Info.Type != ItemType.Weapon) && (Settings.OnlyRefineWeapon))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemCannotBeRefined), Info.Inventory[index].FriendlyName), ChatType.System);
                return;
            }

            if (Info.Inventory[index].Info.Bind.HasFlag(BindMode.DontUpgrade))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemCannotBeRefined), Info.Inventory[index].FriendlyName), ChatType.System);
                return;
            }

            if (Info.Inventory[index].RentalInformation != null && Info.Inventory[index].RentalInformation.BindingFlags.HasFlag(BindMode.DontUpgrade))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemCannotBeRefined), Info.Inventory[index].FriendlyName), ChatType.System);
                return;
            }


            if (index == -1) return;




            //CHECK GOLD HERE
            uint cost = (uint)((Info.Inventory[index].Info.RequiredAmount * 10) * Settings.RefineCost);

            if (cost > Info.Gold)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.NotEnoughGoldToRefine), Info.Inventory[index].FriendlyName), ChatType.System);
                return;
            }

            Info.Gold -= cost;
            Enqueue(new S.LoseGold { Gold = cost });

            //START OF FORMULA

            Info.CurrentRefine = Info.Inventory[index];
            Info.Inventory[index] = null;
            Info.CollectTime = (Envir.Time + (Settings.RefineTime * Settings.Minute));
            Enqueue(new S.RefineItem { UniqueID = uniqueID });


            short orePurity = 0;
            byte oreAmount = 0;
            byte itemAmount = 0;
            short totalDC = 0;
            short totalMC = 0;
            short totalSC = 0;
            short requiredLevel = 0;
            short durability = 0;
            short currentDura = 0;
            short addedStats = 0;
            UserItem ingredient;

            for (int i = 0; i < Info.Refine.Length; i++)
            {
                ingredient = Info.Refine[i];

                if (ingredient == null) continue;
                if (ingredient.Info.Type == ItemType.Weapon)
                {
                    Info.Refine[i] = null;
                    continue;
                }

                if ((ingredient.Info.Stats[Stat.MaxDC] > 0) || (ingredient.Info.Stats[Stat.MaxMC] > 0) || (ingredient.Info.Stats[Stat.MaxSC] > 0))
                {
                    totalDC += (short)(ingredient.Info.Stats[Stat.MinDC] + ingredient.Info.Stats[Stat.MaxDC] + ingredient.AddedStats[Stat.MaxDC]);
                    totalMC += (short)(ingredient.Info.Stats[Stat.MinMC] + ingredient.Info.Stats[Stat.MaxMC] + ingredient.AddedStats[Stat.MaxMC]);
                    totalSC += (short)(ingredient.Info.Stats[Stat.MinSC] + ingredient.Info.Stats[Stat.MaxSC] + ingredient.AddedStats[Stat.MaxSC]);
                    requiredLevel += ingredient.Info.RequiredAmount;
                    if (Math.Floor(ingredient.MaxDura / 1000M) == Math.Floor(ingredient.Info.Durability / 1000M)) durability++;
                    if (Math.Floor(ingredient.CurrentDura / 1000M) == Math.Floor(ingredient.MaxDura / 1000M)) currentDura++;
                    itemAmount++;
                }

                if (ingredient.Info.FriendlyName == Settings.RefineOreName)
                {
                    orePurity += (short)Math.Floor(ingredient.CurrentDura / 1000M);
                    oreAmount++;
                }

                Info.Refine[i] = null;
            }

            if ((totalDC == 0) && (totalMC == 0) && (totalSC == 0))
            {
                Info.CurrentRefine.RefineSuccessChance = 0;
                //Info.CurrentRefine.RefinedValue = RefinedValue.None;
                Info.CurrentRefine.RefineAdded = Settings.RefineIncrease;

                if (Settings.RefineTime == 0)
                {
                    CollectRefine();
                }
                else
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemRefiningCheckLater), Info.CurrentRefine.FriendlyName, Settings.RefineTime), ChatType.System);
                }

                return;
            }

            if (oreAmount == 0)
            {
                Info.CurrentRefine.RefineSuccessChance = 0;
                //Info.CurrentRefine.RefinedValue = RefinedValue.None;
                Info.CurrentRefine.RefineAdded = Settings.RefineIncrease;
                if (Settings.RefineTime == 0)
                {
                    CollectRefine();
                }
                else
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemRefiningCheckLater), Info.CurrentRefine.FriendlyName, Settings.RefineTime), ChatType.System);
                }
                return;
            }


            short refineStat = 0;

            if ((totalDC > totalMC) && (totalDC > totalSC))
            {
                Info.CurrentRefine.RefinedValue = RefinedValue.DC;
                refineStat = totalDC;
            }

            if ((totalMC > totalDC) && (totalMC > totalSC))
            {
                Info.CurrentRefine.RefinedValue = RefinedValue.MC;
                refineStat = totalMC;
            }

            if ((totalSC > totalDC) && (totalSC > totalMC))
            {
                Info.CurrentRefine.RefinedValue = RefinedValue.SC;
                refineStat = totalSC;
            }

            Info.CurrentRefine.RefineAdded = Settings.RefineIncrease;


            int itemSuccess = 0; //Chance out of 35%

            itemSuccess += (refineStat * 5) - Info.CurrentRefine.Info.RequiredAmount;
            itemSuccess += 5;
            if (itemSuccess > 10) itemSuccess = 10;
            if (itemSuccess < 0) itemSuccess = 0; //10%


            if ((requiredLevel / itemAmount) > (Info.CurrentRefine.Info.RequiredAmount - 5)) itemSuccess += 10; //20%
            if (durability == itemAmount) itemSuccess += 10; //30%
            if (currentDura == itemAmount) itemSuccess += 5; //35%

            int oreSuccess = 0; //Chance out of 35%

            if (oreAmount >= itemAmount) oreSuccess += 15; //15%
            if ((orePurity / oreAmount) >= (refineStat / itemAmount)) oreSuccess += 15; //30%
            if (orePurity == refineStat) oreSuccess += 5; //35%

            int luckSuccess = (Info.CurrentRefine.AddedStats[Stat.Luck] + 5); //Chance out of 10%
            if (luckSuccess > 10) luckSuccess = 10;
            if (luckSuccess < 0) luckSuccess = 0;


            int baseSuccess = Settings.RefineBaseChance; //20% as standard

            int successChance = (itemSuccess + oreSuccess + luckSuccess + baseSuccess);

            addedStats = (byte)(Info.CurrentRefine.AddedStats[Stat.MaxDC] + Info.CurrentRefine.AddedStats[Stat.MaxMC] + Info.CurrentRefine.AddedStats[Stat.MaxSC]);
            if (Info.CurrentRefine.Info.Type == ItemType.Weapon) addedStats = (short)(addedStats * Settings.RefineWepStatReduce);
            else addedStats = (short)(addedStats * Settings.RefineItemStatReduce);
            if (addedStats > 50) addedStats = 50;

            successChance -= addedStats;

            Info.CurrentRefine.RefineSuccessChance = successChance;

            //END OF FORMULA

            if (Settings.RefineTime == 0)
            {
                CollectRefine();
            }
            else
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemRefiningCheckLater), Info.CurrentRefine.FriendlyName, Settings.RefineTime), ChatType.System);
            }
        }
        public void CollectRefine()
        {
            S.NPCCollectRefine p = new S.NPCCollectRefine { Success = false };

            if (Info.CurrentRefine == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NotRefiningItems), ChatType.System);
                Enqueue(p);
                return;
            }

            if (Info.CollectTime > Envir.Time)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemReadyInMinutes), Info.CurrentRefine.FriendlyName, ((Info.CollectTime - Envir.Time) / Settings.Minute)), ChatType.System);
                Enqueue(p);
                return;
            }

            int index = -1;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] != null) continue;
                index = i;
                break;
            }

            if (index == -1)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.BagNoRoomForItem), Info.CurrentRefine.FriendlyName), ChatType.System);
                Enqueue(p);
                return;
            }

            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.ItemReturned), ChatType.System);
            p.Success = true;

            GainItem(Info.CurrentRefine);

            Info.CurrentRefine = null;
            Info.CollectTime = 0;
            Enqueue(p);
        }
        public void CheckRefine(ulong uniqueID)
        {
            if (Dead) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCScript.RefineCheckKey, StringComparison.CurrentCultureIgnoreCase))) return;

            int index = -1;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem temp = Info.Inventory[i];
                if (temp == null || temp.UniqueID != uniqueID) continue;
                index = i;
                break;
            }

            if (index == -1) return;

            if (Info.Inventory[index].RefineAdded == 0)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.NoCheckNotRefined), Info.Inventory[index].FriendlyName), ChatType.System);
                return;
            }

            if (Envir.Random.Next(1, 100) > Info.Inventory[index].RefineSuccessChance)
            {
                Info.Inventory[index].RefinedValue = RefinedValue.None;
            }

            if (Envir.Random.Next(1, 100) < Settings.RefineCritChance)
            {
                Info.Inventory[index].RefineAdded = (byte)(Info.Inventory[index].RefineAdded * Settings.RefineCritIncrease);
            }

            if ((Info.Inventory[index].RefinedValue == RefinedValue.DC) && (Info.Inventory[index].RefineAdded > 0))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CongratulationsExtraDC), Info.Inventory[index].FriendlyName, Info.Inventory[index].RefineAdded), ChatType.System);
                Info.Inventory[index].AddedStats[Stat.MaxDC] = (int)Math.Min(int.MaxValue, Info.Inventory[index].AddedStats[Stat.MaxDC] + Info.Inventory[index].RefineAdded);
                Info.Inventory[index].RefineAdded = 0;
                Info.Inventory[index].RefinedValue = RefinedValue.None;
                Info.Inventory[index].RefineSuccessChance = 0;

            }
            else if ((Info.Inventory[index].RefinedValue == RefinedValue.MC) && (Info.Inventory[index].RefineAdded > 0))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CongratulationsExtraMC), Info.Inventory[index].FriendlyName, Info.Inventory[index].RefineAdded), ChatType.System);
                Info.Inventory[index].AddedStats[Stat.MaxMC] = (int)Math.Min(int.MaxValue, Info.Inventory[index].AddedStats[Stat.MaxMC] + Info.Inventory[index].RefineAdded);
                Info.Inventory[index].RefineAdded = 0;
                Info.Inventory[index].RefinedValue = RefinedValue.None;
                Info.Inventory[index].RefineSuccessChance = 0;

            }
            else if ((Info.Inventory[index].RefinedValue == RefinedValue.SC) && (Info.Inventory[index].RefineAdded > 0))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CongratulationsExtraSC), Info.Inventory[index].FriendlyName, Info.Inventory[index].RefineAdded), ChatType.System);
                Info.Inventory[index].AddedStats[Stat.MaxSC] = (int)Math.Min(int.MaxValue, Info.Inventory[index].AddedStats[Stat.MaxSC] + Info.Inventory[index].RefineAdded);
                Info.Inventory[index].RefineAdded = 0;
                Info.Inventory[index].RefinedValue = RefinedValue.None;
                Info.Inventory[index].RefineSuccessChance = 0;
            }
            else if ((Info.Inventory[index].RefinedValue == RefinedValue.None) && (Info.Inventory[index].RefineAdded > 0))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemSmashedOnTest), Info.Inventory[index].FriendlyName), ChatType.System);
                Enqueue(new S.RefineItem { UniqueID = Info.Inventory[index].UniqueID });
                Info.Inventory[index].RefineSuccessChance = 0;
                Info.Inventory[index] = null;
                return;
            }

            Enqueue(new S.ItemUpgraded { Item = Info.Inventory[index] });
            return;
        }

        #endregion
    }
}
