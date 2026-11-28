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
        #region Rental

        public void GetRentedItems()
        {
            Enqueue(new S.GetRentedItems { RentedItems = Info.RentedItems });
        }

        public void ItemRentalRequest()
        {
            if (Dead)
            {
                ReceiveChat(LanguageMgr.Get("UnableRentItemsWhileDead"), ChatType.System);
                return;
            }

            if (ItemRentalPartner != null)
            {
                ReceiveChat(LanguageMgr.Get("YouAreRentingItem"), ChatType.System);
                return;
            }

            var targetPosition = Functions.PointMove(CurrentLocation, Direction, 1);
            if (!CurrentMap.ValidPoint(targetPosition)) return;
            var targetCell = CurrentMap.GetCell(targetPosition);
            PlayerObject targetPlayer = null;

            if (targetCell.Objects == null || targetCell.Objects.Count < 1)
                return;

            foreach (var mapObject in targetCell.Objects)
            {
                if (mapObject.Race != ObjectType.Player)
                    continue;

                targetPlayer = Envir.GetPlayer(mapObject.Name);
            }

            if (targetPlayer == null)
            {
                ReceiveChat(LanguageMgr.Get("FacePlayerToRentItem"), ChatType.System);
                return;
            }

            if (Info.RentedItems.Count >= 3)
            {
                ReceiveChat(LanguageMgr.Get("UnableRentMoreThan3Items"), ChatType.System);
                return;
            }

            if (targetPlayer.Info.HasRentedItem)
            {
                ReceiveChat(LanguageMgr.Get("PlayerUnableToRentItems", targetPlayer.Name), ChatType.System);
                return;
            }

            if (!Functions.FacingEachOther(Direction, CurrentLocation, targetPlayer.Direction,
                targetPlayer.CurrentLocation))
            {
                ReceiveChat(LanguageMgr.Get("FacePlayerToRentItem"), ChatType.System);
                return;
            }

            if (targetPlayer == this)
            {
                ReceiveChat(LanguageMgr.Get("YouCannotRentItemsYourself"), ChatType.System);
                return;
            }

            if (targetPlayer.Dead)
            {
                ReceiveChat(LanguageMgr.Get("UnableRentItemsToPlayerWhileDead", targetPlayer.Name), ChatType.System);
                return;
            }

            if (!Functions.InRange(targetPlayer.CurrentLocation, CurrentLocation, Globals.DataRange)
                || targetPlayer.CurrentMap != CurrentMap)
            {
                ReceiveChat(LanguageMgr.Get("TargetNotInRange", targetPlayer.Name), ChatType.System);
                return;
            }

            if (targetPlayer.ItemRentalPartner != null)
            {
                ReceiveChat(LanguageMgr.Get("PlayerIsBusy", targetPlayer.Name), ChatType.System);
                return;
            }

            ItemRentalPartner = targetPlayer;
            targetPlayer.ItemRentalPartner = this;

            Enqueue(new S.ItemRentalRequest { Name = targetPlayer.Name, Renting = false });
            ItemRentalPartner.Enqueue(new S.ItemRentalRequest { Name = Name, Renting = true });
        }

        public void SetItemRentalFee(uint amount)
        {
            if (ItemRentalFeeLocked)
                return;

            if ((ulong)amount + ItemRentalFeeAmount >= uint.MaxValue)
                return;

            if (Info.Gold < amount)
                return;

            if (ItemRentalPartner == null)
                return;

            ItemRentalFeeAmount += amount;
            Info.Gold -= amount;

            Enqueue(new S.LoseGold { Gold = amount });
            ItemRentalPartner.Enqueue(new S.ItemRentalFee { Amount = amount });
        }

        public void SetItemRentalPeriodLength(uint days)
        {
            if (days < 1 || days > 30)
                return;

            if (ItemRentalItemLocked)
                return;

            if (ItemRentalPartner == null)
                return;

            ItemRentalPeriodLength = days;
            ItemRentalPartner.Enqueue(new S.ItemRentalPeriod { Days = days });
        }

        public void DepositRentalItem(int from, int to)
        {
            var packet = new S.DepositRentalItem { From = from, To = to, Success = false };

            if (ItemRentalItemLocked)
            {
                Enqueue(packet);
                return;
            }

            if (from < 0 || from >= Info.Inventory.Length)
            {
                Enqueue(packet);
                return;
            }

            // TODO: Change this check.
            if (to < 0 || to >= 1)
            {
                Enqueue(packet);
                return;
            }

            var item = Info.Inventory[from];

            if (item == null)
            {
                Enqueue(packet);
                return;
            }

            if (item.RentalInformation?.RentalLocked == true)
            {
                ReceiveChat(LanguageMgr.Get("UnableToRentUntil", item.FriendlyName, item.RentalInformation.ExpiryDate), ChatType.System);
                Enqueue(packet);
                return;
            }

            if (item.Info.Bind.HasFlag(BindMode.UnableToRent))
            {
                ReceiveChat(LanguageMgr.Get("UnableToRent", item.FriendlyName), ChatType.System);
                Enqueue(packet);
                return;
            }

            if (item.RentalInformation != null && item.RentalInformation.BindingFlags.HasFlag(BindMode.UnableToRent))
            {
                ReceiveChat(LanguageMgr.Get("UnableToRentBelongsTo", item.FriendlyName, item.RentalInformation.OwnerName), ChatType.System);
                Enqueue(packet);
                return;
            }

            if (ItemRentalDepositedItem == null)
            {
                ItemRentalDepositedItem = item;
                Info.Inventory[from] = null;

                packet.Success = true;
                UpdateRentalItem();
                Report.ItemMoved(item, MirGridType.Inventory, MirGridType.Renting, from, to);
            }

            Enqueue(packet);
        }

        public void RetrieveRentalItem(int from, int to)
        {
            var packet = new S.RetrieveRentalItem { From = from, To = to, Success = false };

            // TODO: Change this check.
            if (from < 0 || from >= 1)
            {
                Enqueue(packet);
                return;
            }

            if (to < 0 || to >= Info.Inventory.Length)
            {
                Enqueue(packet);
                return;
            }

            var item = ItemRentalDepositedItem;

            if (item == null)
            {
                Enqueue(packet);
                return;
            }

            if (Info.Inventory[to] == null)
            {
                Info.Inventory[to] = item;
                ItemRentalDepositedItem = null;

                packet.Success = true;
                UpdateRentalItem();
                Report.ItemMoved(item, MirGridType.Renting, MirGridType.Inventory, from, to);
            }

            Enqueue(packet);
        }

        private void UpdateRentalItem()
        {
            if (ItemRentalPartner == null)
                return;

            if (ItemRentalDepositedItem != null)
                ItemRentalPartner.CheckItem(ItemRentalDepositedItem);

            ItemRentalPartner.Enqueue(new S.UpdateRentalItem { LoanItem = ItemRentalDepositedItem });
        }

        public void CancelItemRental()
        {
            if (ItemRentalPartner == null)
                return;

            ItemRentalRemoveLocks();

            var rentalPair = new[]  {
                ItemRentalPartner,
                this
            };

            for (var i = 0; i < 2; i++)
            {
                if (rentalPair[i] == null)
                    continue;

                if (rentalPair[i].ItemRentalDepositedItem != null)
                {
                    var item = rentalPair[i].ItemRentalDepositedItem;

                    if (FreeSpace(rentalPair[i].Info.Inventory) < 1)
                    {
                        rentalPair[i].GainItemMail(item, 1);
                        rentalPair[i].Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentalPair[i].ItemRentalDepositedItem = null;

                        Report.ItemMailed(item, item.Count, 1);

                        continue;
                    }

                    for (var j = 0; j < rentalPair[i].Info.Inventory.Length; j++)
                    {
                        if (rentalPair[i].Info.Inventory[j] != null)
                            continue;

                        if (rentalPair[i].CanGainItem(item))
                            rentalPair[i].RetrieveRentalItem(0, j);
                        else
                        {
                            rentalPair[i].GainItemMail(item, 1);
                            rentalPair[i].Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                            Report.ItemMailed(item, item.Count, 1);
                        }

                        rentalPair[i].ItemRentalDepositedItem = null;

                        break;
                    }
                }

                if (rentalPair[i].ItemRentalFeeAmount > 0)
                {
                    rentalPair[i].GainGold(rentalPair[i].ItemRentalFeeAmount);
                    rentalPair[i].ItemRentalFeeAmount = 0;

                    Report.GoldChanged(rentalPair[i].ItemRentalFeeAmount, false);
                }

                rentalPair[i].ItemRentalPartner = null;
                rentalPair[i].Enqueue(new S.CancelItemRental());
            }
        }

        public void ItemRentalLockFee()
        {
            S.ItemRentalLock p = new S.ItemRentalLock { Success = false, GoldLocked = false, ItemLocked = false };

            if (ItemRentalFeeAmount > 0)
            {
                ItemRentalFeeLocked = true;
                p.GoldLocked = true;
                p.Success = true;

                ItemRentalPartner.Enqueue(new S.ItemRentalPartnerLock { GoldLocked = ItemRentalFeeLocked });
            }

            if (ItemRentalFeeLocked && ItemRentalPartner.ItemRentalItemLocked)
                ItemRentalPartner.Enqueue(new S.CanConfirmItemRental());
            else if (ItemRentalFeeLocked && !ItemRentalPartner.ItemRentalItemLocked)
                ItemRentalPartner.ReceiveChat(LanguageMgr.Get("PlayerLockedRentalFee", Name), ChatType.System);

            Enqueue(p);
        }

        public void ItemRentalLockItem()
        {
            S.ItemRentalLock p = new S.ItemRentalLock { Success = false, GoldLocked = false, ItemLocked = false };

            if (ItemRentalDepositedItem != null)
            {
                ItemRentalItemLocked = true;
                p.ItemLocked = true;
                p.Success = true;

                ItemRentalPartner.Enqueue(new S.ItemRentalPartnerLock { ItemLocked = ItemRentalItemLocked });
            }

            if (ItemRentalItemLocked && ItemRentalPartner.ItemRentalFeeLocked)
                Enqueue(new S.CanConfirmItemRental());
            else if (ItemRentalItemLocked && !ItemRentalPartner.ItemRentalFeeLocked)
                ItemRentalPartner.ReceiveChat(LanguageMgr.Get("PlayerLockedRentalItem", Name), ChatType.System);


            Enqueue(p);
        }

        private void ItemRentalRemoveLocks()
        {
            ItemRentalFeeLocked = false;
            ItemRentalItemLocked = false;

            if (ItemRentalPartner == null)
                return;

            ItemRentalPartner.ItemRentalFeeLocked = false;
            ItemRentalPartner.ItemRentalItemLocked = false;
        }

        public void ConfirmItemRental()
        {
            if (ItemRentalPartner == null)
            {
                CancelItemRental();
                return;
            }

            if (Info.RentedItems.Count >= 3)
            {
                CancelItemRental();
                return;
            }

            if (ItemRentalPartner.Info.HasRentedItem)
            {
                CancelItemRental();
                return;
            }

            if (ItemRentalDepositedItem == null)
                return;

            if (ItemRentalPartner.ItemRentalFeeAmount <= 0)
                return;

            if (ItemRentalDepositedItem.Info.Bind.HasFlag(BindMode.UnableToRent))
                return;

            if (ItemRentalDepositedItem.RentalInformation != null &&
                ItemRentalDepositedItem.RentalInformation.BindingFlags.HasFlag(BindMode.UnableToRent))
                return;

            if (!Functions.InRange(ItemRentalPartner.CurrentLocation, CurrentLocation, Globals.DataRange)
                || ItemRentalPartner.CurrentMap != CurrentMap || !Functions.FacingEachOther(Direction, CurrentLocation,
                    ItemRentalPartner.Direction, ItemRentalPartner.CurrentLocation))
            {
                CancelItemRental();
                return;
            }

            if (!ItemRentalItemLocked && !ItemRentalPartner.ItemRentalFeeLocked)
                return;

            if (!ItemRentalPartner.CanGainItem(ItemRentalDepositedItem))
            {
                ReceiveChat(LanguageMgr.Get("PlayerUnableReceiveItem", ItemRentalPartner.Name), ChatType.System);
                Enqueue(new S.CancelItemRental());

                ItemRentalPartner.ReceiveChat(LanguageMgr.Get("UnableToAcceptRentalItem"), ChatType.System);
                ItemRentalPartner.Enqueue(new S.CancelItemRental());

                return;
            }

            if (!CanGainGold(ItemRentalPartner.ItemRentalFeeAmount))
            {
                ReceiveChat(LanguageMgr.Get("YouCannotReceiveMoreGold"), ChatType.System);
                Enqueue(new S.CancelItemRental());

                ItemRentalPartner.ReceiveChat(LanguageMgr.Get("PlayerUnableReceiveGold", Name), ChatType.System);
                ItemRentalPartner.Enqueue(new S.CancelItemRental());

                return;
            }

            var item = ItemRentalDepositedItem;
            item.RentalInformation = new RentalInformation
            {
                OwnerName = Name,
                ExpiryDate = Envir.Now.AddDays(ItemRentalPeriodLength),
                BindingFlags = BindMode.DontDrop | BindMode.DontStore | BindMode.DontSell | BindMode.DontTrade | BindMode.UnableToRent | BindMode.DontUpgrade | BindMode.UnableToDisassemble
            };

            var itemRentalInformation = new ItemRentalInformation
            {
                ItemId = item.UniqueID,
                ItemName = item.FriendlyName,
                RentingPlayerName = ItemRentalPartner.Name,
                ItemReturnDate = item.RentalInformation.ExpiryDate,

            };

            Info.RentedItems.Add(itemRentalInformation);
            ItemRentalDepositedItem = null;

            ItemRentalPartner.GainItem(item);
            ItemRentalPartner.Info.HasRentedItem = true;
            ItemRentalPartner.ReceiveChat(LanguageMgr.Get("YouHaveRentedFromUntil", item.FriendlyName, Name, item.RentalInformation.ExpiryDate), ChatType.System);

            GainGold(ItemRentalPartner.ItemRentalFeeAmount);
            ReceiveChat(LanguageMgr.Get("ReceivedGoldForItemRental", ItemRentalPartner.ItemRentalFeeAmount), ChatType.System);
            ItemRentalPartner.ItemRentalFeeAmount = 0;

            Enqueue(new S.ConfirmItemRental());
            ItemRentalPartner.Enqueue(new S.ConfirmItemRental());

            ItemRentalRemoveLocks();

            ItemRentalPartner.ItemRentalPartner = null;
            ItemRentalPartner = null;
        }

        #endregion
    }
}
