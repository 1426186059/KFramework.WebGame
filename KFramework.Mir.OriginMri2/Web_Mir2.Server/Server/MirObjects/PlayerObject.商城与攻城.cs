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
        #region Gameshop

        public void GameShopStock(GameShopItemTable item)
        {
            int purchased;
            int StockLevel;

            if (item.iStock) //Invididual Stock
            {
                Info.GSpurchases.TryGetValue(item.Info.Index, out purchased);
            }
            else //Server Stock
            {
                Envir.GameshopLog.TryGetValue(item.Info.Index, out purchased);
            }

            if (item.Stock - purchased >= 0)
            {
                StockLevel = item.Stock - purchased;
                Enqueue(new S.GameShopStock { GIndex = item.Info.Index, StockLevel = StockLevel });
            }

        }

        public void GameshopBuy(int GIndex, byte Quantity, int PType)
        {
            if (Quantity < 1 || Quantity > 99) return;

            List<GameShopItemTable> shopList = Envir.GameShopList;
            GameShopItemTable Product = null;

            int purchased;
            bool stockAvailable = false;
            bool canAfford = false;
            uint CreditCost = 0;
            uint GoldCost = 0;

            List<UserItem> mailItems = new List<UserItem>();

            for (int i = 0; i < shopList.Count; i++)
            {
                if (shopList[i].GIndex == GIndex)
                {
                    Product = shopList[i];
                    break;
                }
            }

            if (Product == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouBuyItemNotInShop), ChatType.System);
                MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerBuyNonexistentItem), Info.Name));
                return;
            }

            if (((decimal)(Quantity * Product.Count) / Product.Info.StackSize) > 5) return;

            if (Product.Stock != 0)
            {

                if (Product.iStock) //Invididual Stock
                {
                    Info.GSpurchases.TryGetValue(Product.Info.Index, out purchased);
                }
                else //Server Stock
                {
                    Envir.GameshopLog.TryGetValue(Product.Info.Index, out purchased);
                }

                if (Product.Stock - purchased - Quantity >= 0)
                {
                    stockAvailable = true;
                }
                else
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouBuyMoreThanAvailable), ChatType.System);
                    GameShopStock(Product);
                    MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerBuyItemStockUnavailable), Info.Name, Product.Info.FriendlyName, Quantity));
                    return;
                }
            }
            else
            {
                stockAvailable = true;
            }

            if (stockAvailable)
            {
                MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerBuyItemStockAvailable), Info.Name, Product.Info.FriendlyName, Quantity));

                if (PType == 0)
                {
                    var cost = Product.CreditPrice * Quantity;
                    if (Product.CanBuyCredit && cost <= Account.Credit)
                    {
                        canAfford = true;
                        CreditCost = cost;
                    }
                }
                else if (PType == 1)
                {
                    var goldcost = Product.GoldPrice * Quantity;
                    if (Product.CanBuyGold && goldcost <= Account.Gold)
                    {
                        canAfford = true;
                        GoldCost = goldcost;
                    }
                }
                else
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouDontHaveEnoughCurrency), ChatType.System);
                    MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerBuyItemNotEnoughCurrency), Info.Name, Product.Info.FriendlyName, Quantity));
                    return;
                }
            }
            else
            {
                return;
            }

            if (canAfford)
            {
                MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerBuyItemEnoughCurrency), Info.Name, Product.Info.FriendlyName, Quantity, PType));
                if (PType == 0)
                {
                    Account.Credit -= CreditCost;
                    Report.CreditChanged(CreditCost, true, Product.Info.FriendlyName);
                    if (CreditCost != 0) Enqueue(new S.LoseCredit { Credit = CreditCost });

                }
                if (PType == 1)
                {
                    Account.Gold -= GoldCost;
                    Report.GoldChanged(GoldCost, true, Product.Info.FriendlyName);
                    if (GoldCost != 0) Enqueue(new S.LoseGold { Gold = GoldCost });
                }

                if (Product.iStock && Product.Stock != 0)
                {
                    Info.GSpurchases.TryGetValue(Product.Info.Index, out purchased);
                    if (purchased == 0)
                    {
                        Info.GSpurchases[Product.GIndex] = Quantity;
                    }
                    else
                    {
                        Info.GSpurchases[Product.GIndex] += Quantity;
                    }
                }

                Envir.GameshopLog.TryGetValue(Product.Info.Index, out purchased);
                if (purchased == 0)
                {
                    Envir.GameshopLog[Product.GIndex] = Quantity;
                }
                else
                {
                    Envir.GameshopLog[Product.GIndex] += Quantity;
                }

                if (Product.Stock != 0) GameShopStock(Product);
            }
            else
            {
                return;
            }

            Report.ItemGSBought(Product, Quantity, CreditCost, GoldCost);

            ushort quantity = (ushort)(Quantity * Product.Count);

            if (Product.Info.StackSize <= 1 || quantity == 1)
            {
                for (int i = 0; i < Quantity; i++)
                {
                    UserItem mailItem = Envir.CreateFreshItem(Envir.GetItemInfo(Product.Info.Index));

                    mailItems.Add(mailItem);
                }
            }
            else
            {
                while (quantity > 0)
                {
                    UserItem mailItem = Envir.CreateFreshItem(Envir.GetItemInfo(Product.Info.Index));
                    mailItem.Count = 0;
                    for (int i = 0; i < mailItem.Info.StackSize; i++)
                    {
                        mailItem.Count++;
                        quantity--;
                        if (quantity == 0) break;
                    }
                    if (mailItem.Count == 0) break;

                    mailItems.Add(mailItem);

                }
            }

            MailInfo mail = new MailInfo(Info.Index)
            {
                MailID = ++Envir.NextMailID,
                Sender = "Gameshop",
                Message = GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.ThankYouPurchaseGameshop),
                Items = mailItems,
            };
            mail.Send();

            MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerBuyingItemsSent), Info.Name, Product.Info.FriendlyName, Quantity));
            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.PurchasesSentMailbox), ChatType.System);
        }

        public void GetGameShop()
        {
            int purchased;
            int stockLevel;

            for (int i = 0; i < Envir.GameShopList.Count; i++)
            {
                var item = Envir.GameShopList[i];

                if (item.Stock != 0)
                {
                    if (item.iStock) //Individual Stock
                    {
                        Info.GSpurchases.TryGetValue(item.Info.Index, out purchased);
                    }
                    else //Server Stock
                    {
                        Envir.GameshopLog.TryGetValue(item.Info.Index, out purchased);
                    }

                    if (item.Stock - purchased >= 0)
                    {
                        stockLevel = item.Stock - purchased;
                        Enqueue(new S.GameShopInfo { Item = item, StockLevel = stockLevel });
                    }
                }
                else
                {
                    Enqueue(new S.GameShopInfo { Item = item, StockLevel = item.Stock });
                }
            }
        }

        #endregion

        #region ConquestWall
        public void CheckConquest(bool checkPalace = false)
        {
            if (CurrentMap.tempConquest == null && CurrentMap.Conquest != null)
            {
                ConquestObject swi = CurrentMap.GetConquest(CurrentLocation);
                if (swi != null)
                    EnterSabuk();
                else
                    LeaveSabuk();
            }
            else if (CurrentMap.tempConquest != null)
            {
                if (checkPalace && CurrentMap.Info.Index == CurrentMap.tempConquest.PalaceMap.Info.Index && CurrentMap.tempConquest.GameType == ConquestGame.CapturePalace)
                    CurrentMap.tempConquest.TakeConquest(this);

                EnterSabuk();
            }
        }
        public void EnterSabuk()
        {
            if (WarZone) return;
            WarZone = true;
            RefreshNameColour();
        }

        public void LeaveSabuk()
        {
            if (!WarZone) return;
            WarZone = false;
            RefreshNameColour();
        }
        #endregion
    }
}
