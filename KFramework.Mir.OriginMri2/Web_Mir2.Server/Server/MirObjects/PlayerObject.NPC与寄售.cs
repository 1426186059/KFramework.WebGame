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
        #region NPC

        public void CallDefaultNPC(DefaultNPCType type, params object[] value)
        {
            string key = string.Empty;

            switch (type)
            {
                case DefaultNPCType.Login:
                    key = "Login";
                    break;
                case DefaultNPCType.UseItem:
                    if (value.Length < 1) return;
                    key = string.Format("UseItem({0})", value[0]);
                    break;
                case DefaultNPCType.Trigger:
                    if (value.Length < 1) return;
                    key = string.Format("Trigger({0})", value[0]);
                    break;
                case DefaultNPCType.MapCoord:
                    if (value.Length < 3) return;
                    key = string.Format("MapCoord({0},{1},{2})", value[0], value[1], value[2]);
                    break;
                case DefaultNPCType.MapEnter:
                    if (value.Length < 1) return;
                    key = string.Format("MapEnter({0})", value[0]);
                    break;
                case DefaultNPCType.Die:
                    key = "Die";
                    break;
                case DefaultNPCType.LevelUp:
                    key = "LevelUp";
                    break;
                case DefaultNPCType.CustomCommand:
                    if (value.Length < 1) return;
                    key = string.Format("CustomCommand({0})", value[0]);
                    break;
                case DefaultNPCType.OnAcceptQuest:
                    if (value.Length < 1) return;
                    key = string.Format("OnAcceptQuest({0})", value[0]);
                    break;
                case DefaultNPCType.OnFinishQuest:
                    if (value.Length < 1) return;
                    key = string.Format("OnFinishQuest({0})", value[0]);
                    break;
                case DefaultNPCType.Daily:
                    key = "Daily";
                    Info.NewDay = false;
                    break;
                case DefaultNPCType.Client:
                    key = "Client";
                    break;
            }

            key = string.Format("[@_{0}]", key);

            DelayedAction action = new DelayedAction(DelayedType.NPC, Envir.Time, Envir.DefaultNPC.LoadedObjectID, Envir.DefaultNPC.ScriptID, key);
            ActionList.Add(action);

            Enqueue(new S.NPCUpdate { NPCID = Envir.DefaultNPC.LoadedObjectID });
        }

        public void CallDefaultNPC(string key)
        {
            if (NPCObjectID != Envir.DefaultNPC.LoadedObjectID) return;

            var script = NPCScript.Get(NPCScriptID);
            script.Call(this, NPCObjectID, key.ToUpper());

            CallNPCNextPage();
        }

        public void CallNPC(uint objectID, string key, bool openQuestPanel = false)
        {
            if (Dead)
            {
                MessageQueue.Enqueue($"[NPC诊断] CallNPC 被拒: 玩家已死亡");
                return;
            }

            // 原始传奇 checknpcTaskDialog：仅"任务待接/待交且点的是该任务 NPC"时要求弹任务面板。
            // 由调用方（任务追踪入口）按任务 state 判定后传入，不用跨帧残留标记。
            OpenQuestPanelOnNextNPCResponse = openQuestPanel;

            // 先告知客户端当前交互的 NPC（GameScene.NPCID），否则 S.NPCResponse 处理时取不到 NPC，
            // 任务面板/对话面板的"与谁对话"判定会失效，导致面板不弹出。
            Enqueue(new S.NPCUpdate { NPCID = objectID });

            key = key.ToUpper();
            MessageQueue.Enqueue($"[NPC诊断] Player.CallNPC: objectID={objectID} key={key} Map={CurrentMap.Info.Index} NPCs.Count={CurrentMap.NPCs.Count} NPCObjectID(本次交互)={NPCObjectID}");

            bool found = false;
            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                NPCObject ob = CurrentMap.NPCs[i];
                if (ob.ObjectID != objectID) continue;
                found = true;
                bool inRange = Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange);
                ob.CheckVisible(this);
                bool vis = ob.VisibleLog.ContainsKey(Info.Index) && ob.VisibleLog[Info.Index] && ob.Visible;
                MessageQueue.Enqueue($"[NPC诊断] 匹配到 NPC: Name={ob.Name} ObjectID={ob.ObjectID} InRange={inRange} Visible={vis} ScriptID={ob.ScriptID} NPCInfo.Index={ob.Info.Index}");
                if (!inRange)
                {
                    MessageQueue.Enqueue($"[NPC诊断] 距离超限 (DataRange={Globals.DataRange})，return");
                    return;
                }
                if (!vis)
                {
                    MessageQueue.Enqueue($"[NPC诊断] NPC 不可见，return");
                    return;
                }

                var scriptID = NPCScriptID;
                if (objectID != NPCObjectID || key == NPCScript.MainKey)
                {
                    scriptID = ob.ScriptID;
                }

                var script = NPCScript.Get(scriptID);
                MessageQueue.Enqueue($"[NPC诊断] NPCScript.Get(scriptID={scriptID}) => {(script == null ? "null" : "OK")}");
                if (script != null)
                    MessageQueue.Enqueue($"[NPC诊断] 脚本 FileName={script.FileName}");
                script.Call(this, objectID, key);

                break;
            }

            if (!found) MessageQueue.Enqueue($"[NPC诊断] 未在 CurrentMap.NPCs 找到 ObjectID={objectID}（可能来自其他地图或客户端发错 ID）");

            // 本次没有产生 NPCResponse 时也要清除，避免误影响下一次普通对话。
            OpenQuestPanelOnNextNPCResponse = false;
            CallNPCNextPage();
        }
        private void CallNPCNextPage()
        {
            //process any new npc calls immediately
            for (int i = 0; i < ActionList.Count; i++)
            {
                if (ActionList[i].Type != DelayedType.NPC || ActionList[i].Time != -1) continue;
                var action = ActionList[i];

                ActionList.RemoveAt(i);

                CompleteNPC(action.Params);
            }
        }

        public void BuyItem(ulong index, ushort count, PanelType type)
        {
            if (Dead || count < 1) return;

            if (NPCPage == null ||
                !(String.Equals(NPCPage.Key, NPCScript.BuySellKey, StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(NPCPage.Key, NPCScript.BuyKey, StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(NPCPage.Key, NPCScript.BuyBackKey, StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(NPCPage.Key, NPCScript.BuyUsedKey, StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(NPCPage.Key, NPCScript.PearlBuyKey, StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(NPCPage.Key, NPCScript.BuyNewKey, StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(NPCPage.Key, NPCScript.BuySellNewKey, StringComparison.CurrentCultureIgnoreCase)))
            {
                MessageQueue.Enqueue($"[商店诊断] BuyItem 被白名单拦截 index={index} NPCPage={NPCPage?.Key ?? "null"}");
                ReceiveOutputMessage("购买失败，当前对话不允许购买物品。", OutputMessageType.Error);
                return;
            }
            MessageQueue.Enqueue($"[商店诊断] BuyItem 通过 index={index} count={count} NPCPage={NPCPage.Key}");

            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                NPCObject ob = CurrentMap.NPCs[i];
                if (ob.ObjectID != NPCObjectID) continue;
                if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                if (type == PanelType.Buy)
                {
                    NPCScript script = NPCScript.Get(NPCScriptID);
                    script.Buy(this, index, count);

                    // 原始传奇532买物品引导任务：买到本职业技能书 → 书入包、任务置"完成待交"，
                    // 弹 NPC 任务面板；由玩家点面板"领取奖励"才真正完成并发奖、推进任务15。
                    CheckBuyGoalQuests(fromPurchase: true, boughtFromNPC: ob.ObjectID);
                    // 只看 BuyGoalDone：它在 CheckBuyGoalQuests 里已确认 CheckCompleted 通过，
                    // 不再额外要求 Completed（避免状态同步时序差异导致不切面板）。
                    bool buyGoalPending = CurrentQuests.Any(q => q.Info != null && q.Info.goaltype == 532 && q.BuyGoalDone);
                    MessageQueue.Enqueue($"[商店诊断] 购买后 532 目标达成={buyGoalPending}，切换任务面板={buyGoalPending}");
                    if (buyGoalPending)
                        CallNPC(ob.ObjectID, NPCScript.MainKey, openQuestPanel: true); // 商店面板 → NPC任务面板
                }
            }
        }
        public void CraftItem(ulong index, ushort count, int[] slots)
        {
            if (Dead || count < 1) return;

            if (NPCPage == null) return;

            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                NPCObject ob = CurrentMap.NPCs[i];
                if (ob.ObjectID != NPCObjectID) continue;
                if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                NPCScript script = NPCScript.Get(NPCScriptID);
                script.Craft(this, index, count, slots);
            }
        }


        public void SellItem(ulong uniqueID, ushort count)
        {
            S.SellItem p = new S.SellItem { UniqueID = uniqueID, Count = count };

            if (Dead || count == 0)
            {
                Enqueue(p);
                return;
            }

            if (NPCPage == null || !(String.Equals(NPCPage.Key, NPCScript.BuySellKey, StringComparison.CurrentCultureIgnoreCase) || String.Equals(NPCPage.Key, NPCScript.SellKey, StringComparison.CurrentCultureIgnoreCase)))
            {
                Enqueue(p);
                return;
            }

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCObjectID) continue;
                if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                UserItem temp = null;
                int index = -1;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    temp = Info.Inventory[i];
                    if (temp == null || temp.UniqueID != uniqueID) continue;
                    index = i;
                    break;
                }

                if (temp == null || index == -1 || count > temp.Count)
                {
                    Enqueue(p);
                    return;
                }

                if (temp.Info.Bind.HasFlag(BindMode.DontSell))
                {
                    Enqueue(p);
                    return;
                }

                if (temp.RentalInformation != null && temp.RentalInformation.BindingFlags.HasFlag(BindMode.DontSell))
                {
                    Enqueue(p);
                    return;
                }

                NPCScript script = NPCScript.Get(NPCScriptID);

                if (script.Types.Count != 0 && !script.Types.Contains(temp.Info.Type))
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotSellItemHere), ChatType.System);
                    Enqueue(p);
                    return;
                }

                if (temp.Info.StackSize > 1 && count != temp.Count)
                {
                    UserItem item = Envir.CreateFreshItem(temp.Info);
                    item.Count = count;

                    if (item.Price() / 2 + Account.Gold > uint.MaxValue)
                    {
                        Enqueue(p);
                        return;
                    }

                    temp.Count -= count;
                    temp = item;
                }
                else Info.Inventory[index] = null;

                script.Sell(this, temp);

                if (Settings.GoodsOn)
                {
                    var callingNPC = NPCObject.Get(NPCObjectID);

                    if (callingNPC != null)
                    {
                        if (!callingNPC.BuyBack.ContainsKey(Name)) callingNPC.BuyBack[Name] = new List<UserItem>();

                        if (Settings.GoodsBuyBackMaxStored > 0 && callingNPC.BuyBack[Name].Count >= Settings.GoodsBuyBackMaxStored)
                            callingNPC.BuyBack[Name].RemoveAt(0);

                        temp.BuybackExpiryDate = Envir.Now;
                        callingNPC.BuyBack[Name].Add(temp);
                    }
                }

                p.Success = true;
                Enqueue(p);
                GainGold(temp.Price() / 2);
                RefreshBagWeight();

                return;
            }

            Enqueue(p);
        }
        public void RepairItem(ulong uniqueID, bool special = false)
        {
            Enqueue(new S.RepairItem { UniqueID = uniqueID });

            if (Dead) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCScript.RepairKey, StringComparison.CurrentCultureIgnoreCase) && !special) || (!String.Equals(NPCPage.Key, NPCScript.SRepairKey, StringComparison.CurrentCultureIgnoreCase) && special)) return;

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCObjectID) continue;
                if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                UserItem temp = null;
                int index = -1;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    temp = Info.Inventory[i];
                    if (temp == null || temp.UniqueID != uniqueID) continue;
                    index = i;
                    break;
                }

                if (temp == null || index == -1) return;

                if ((temp.Info.Bind.HasFlag(BindMode.DontRepair)) || (temp.Info.Bind.HasFlag(BindMode.NoSRepair) && special))
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotRepairItem), ChatType.System);
                    return;
                }

                NPCScript script = NPCScript.Get(NPCScriptID);

                if (script.Types.Count != 0 && !script.Types.Contains(temp.Info.Type))
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotRepairItemHere), ChatType.System);
                    return;
                }

                uint cost;
                uint baseCost;
                if (!special)
                {
                    cost = (uint)(temp.RepairPrice() * script.PriceRate(this));
                    baseCost = (uint)(temp.RepairPrice() * script.PriceRate(this, true));
                }
                else
                {
                    cost = (uint)(temp.RepairPrice() * 3 * script.PriceRate(this));
                    baseCost = (uint)(temp.RepairPrice() * 3 * script.PriceRate(this, true));
                }

                if (cost > Account.Gold) return;

                Account.Gold -= cost;
                Enqueue(new S.LoseGold { Gold = cost });
                if (ob.Conq != null) ob.Conq.GuildInfo.GoldStorage += (cost - baseCost);

                if (!special) temp.MaxDura = (ushort)Math.Max(0, temp.MaxDura - (temp.MaxDura - temp.CurrentDura) / 30);

                temp.CurrentDura = temp.MaxDura;
                temp.DuraChanged = false;

                Enqueue(new S.ItemRepaired { UniqueID = uniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });
                return;
            }
        }
        public void SendStorage()
        {
            if (Settings.RequireStoragePassword && Account != null && Account.HasStoragePassword && !StorageUnlocked)
            {
                Connection.StorageSent = false;
                return;
            }

            if (Connection.StorageSent) return;
            Connection.StorageSent = true;

            for (int i = 0; i < Account.Storage.Length; i++)
            {
                UserItem item = Account.Storage[i];
                if (item == null) continue;
                //CheckItemInfo(item.Info);
                CheckItem(item);
            }

            Enqueue(new S.UserStorage { Storage = Account.Storage }); // Should be no alter before being sent.
        }

        public void ResetStorageUnlock()
        {
            StorageUnlocked = false;
        }

        public void SetStorageUnlocked(bool unlocked)
        {
            StorageUnlocked = unlocked;
        }

        public bool TryUnlockStorage(string password)
        {
            if (Account == null) return false;
            if (!Account.HasStoragePassword)
            {
                StorageUnlocked = true;
                return true;
            }

            if (!Account.ValidateStoragePassword(password)) return false;

            StorageUnlocked = true;
            return true;
        }

        private bool CanAccessStorage()
        {
            if (!Settings.RequireStoragePassword) return true;

            return Account == null || !Account.HasStoragePassword || StorageUnlocked;
        }

        #endregion

        #region Consignment
        public void ConsignItem(ulong uniqueID, uint price, MarketPanelType panelType)
        {
            S.ConsignItem p = new S.ConsignItem { UniqueID = uniqueID };

            if (Dead || NPCPage == null)
            {
                Enqueue(p);
                return;
            }

            switch (panelType)
            {
                case MarketPanelType.Consign:
                    {
                        if (price < Globals.MinConsignment || price > Globals.MaxConsignment)
                        {
                            Enqueue(p);
                            return;
                        }

                        if (Account.Gold < Globals.ConsignmentCost)
                        {
                            Enqueue(p);
                            return;
                        }
                    }
                    break;
                case MarketPanelType.Auction:
                    {
                        if (price < Globals.MinStartingBid || price > Globals.MaxStartingBid)
                        {
                            Enqueue(p);
                            return;
                        }

                        if (Account.Gold < Globals.AuctionCost)
                        {
                            Enqueue(p);
                            return;
                        }
                    }
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCObjectID) continue;
                if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                UserItem temp = null;
                int index = -1;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    temp = Info.Inventory[i];
                    if (temp == null || temp.UniqueID != uniqueID) continue;
                    index = i;
                    break;
                }

                if (temp == null || index == -1)
                {
                    Enqueue(p);
                    return;
                }

                if (temp.Info.Bind.HasFlag(BindMode.DontSell))
                {
                    Enqueue(p);
                    return;
                }

                MarketItemType type = panelType == MarketPanelType.Consign ? MarketItemType.Consign : MarketItemType.Auction;
                uint cost = panelType == MarketPanelType.Consign ? Globals.ConsignmentCost : Globals.AuctionCost;

                //TODO Check Max Consignment.

                AuctionInfo auction = new AuctionInfo(Info, temp, price, type);

                Account.Auctions.AddLast(auction);
                Envir.Auctions.AddFirst(auction);

                p.Success = true;
                Enqueue(p);

                Info.Inventory[index] = null;

                Account.Gold -= cost;

                Enqueue(new S.LoseGold { Gold = cost });
                RefreshBagWeight();
            }

            Enqueue(p);
        }

        private bool Match(AuctionInfo info)
        {
            return (UserMatch || !info.Expired && !info.Sold)
                && (!UserMatch || ((MarketPanelType == MarketPanelType.Auction && info.ItemType == MarketItemType.Auction) || (MarketPanelType != MarketPanelType.Auction && info.ItemType == MarketItemType.Consign)))
                && ((MatchType == ItemType.Nothing || info.Item.Info.Type == MatchType)
                && (info.Item.Info.Shape >= MinShapes && info.Item.Info.Shape <= MaxShapes)
                && (string.IsNullOrWhiteSpace(MatchName) || info.Item.Info.Name.Replace(" ", "").IndexOf(MatchName, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public void MarketPage(int page)
        {
            if (Dead || Envir.Time < SearchTime) return;

            if (MarketPanelType != MarketPanelType.GameShop)
            {
                bool failed = true;

                if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCScript.MarketKey, StringComparison.CurrentCultureIgnoreCase)) || page <= PageSent) return;

                SearchTime = Envir.Time + Globals.SearchDelay;

                for (int n = 0; n < CurrentMap.NPCs.Count; n++)
                {
                    NPCObject ob = CurrentMap.NPCs[n];
                    if (ob.ObjectID != NPCObjectID) continue;
                    if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;
                    failed = false;
                }

                if (failed)
                {
                    return;
                }
            }

            List<AuctionInfo> listings = new List<AuctionInfo>();
            List<ClientAuction> clientListings = new List<ClientAuction>();

            for (int i = 0; i < 10; i++)
            {
                if (i + page * 10 >= Search.Count) break;
                listings.Add(Search[i + page * 10]);
            }

            foreach (var listing in listings)
            {
                clientListings.Add(listing.CreateClientAuction(UserMatch));
            }

            for (int i = 0; i < listings.Count; i++)
            {
                CheckItem(listings[i].Item);
            }

            PageSent = page;
            Enqueue(new S.NPCMarketPage { Listings = clientListings });
        }

        public void GetMarket(string name, ItemType type)
        {
            Search.Clear();
            MatchName = name.Replace(" ", "");
            MatchType = type;
            PageSent = 0;

            long start = Envir.Stopwatch.ElapsedMilliseconds;

            if (MarketPanelType == MarketPanelType.GameShop)
            {
                //Search = Envir.GameShopList.Where(x => (MatchType == ItemType.Nothing || x.Info.Type == MatchType)
                //&& (x.Info.Shape >= MinShapes && x.Info.Shape <= MaxShapes)
                //&& (string.IsNullOrWhiteSpace(MatchName) || x.Info.Name.Replace(" ", "").IndexOf(MatchName, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            }
            else
            {
                LinkedListNode<AuctionInfo> current = UserMatch ? Account.Auctions.First : Envir.Auctions.First;

                while (current != null)
                {
                    if (Match(current.Value)) Search.Add(current.Value);
                    current = current.Next;
                }
            }

            List<AuctionInfo> listings = new List<AuctionInfo>();
            List<ClientAuction> clientListings = new List<ClientAuction>();

            for (int i = 0; i < 10; i++)
            {
                if (i >= Search.Count) break;
                listings.Add(Search[i]);
            }

            foreach (var listing in listings)
            {
                clientListings.Add(listing.CreateClientAuction(UserMatch));
            }

            for (int i = 0; i < listings.Count; i++)
                CheckItem(listings[i].Item);

            Enqueue(new S.NPCMarket { Listings = clientListings, Pages = (Search.Count - 1) / 10 + 1, UserMode = UserMatch });

            MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.MsToMatchItems), Envir.Stopwatch.ElapsedMilliseconds - start, MarketPanelType == MarketPanelType.GameShop ? Envir.GameShopList.Count : (UserMatch ? Account.Auctions.Count : Envir.Auctions.Count)));
        }

        public void MarketSearch(string match, ItemType type)
        {
            if (Dead || Envir.Time < SearchTime) return;

            SearchTime = Envir.Time + Globals.SearchDelay;

            if (MarketPanelType == MarketPanelType.GameShop)
            {
                GetMarket(match, type);
            }
            else
            {
                if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.MarketKey, StringComparison.CurrentCultureIgnoreCase)) return;

                for (int n = 0; n < CurrentMap.NPCs.Count; n++)
                {
                    NPCObject ob = CurrentMap.NPCs[n];
                    if (ob.ObjectID != NPCObjectID) continue;

                    if (!Functions.InRange(CurrentLocation, ob.CurrentLocation, Globals.DataRange)) return;

                    GetMarket(match, type);
                }
            }
        }

        public void MarketBuy(ulong auctionID, uint bidPrice = 0)
        {
            if (Dead)
            {
                Enqueue(new S.MarketFail { Reason = 0 });
                return;
            }

            if (MarketPanelType == MarketPanelType.GameShop)
            {
                foreach (AuctionInfo auction in Search)
                {
                    if (auction.AuctionID != auctionID) continue;
                    if (auction.ItemType != MarketItemType.GameShop) continue;

                    if (auction.Price > Account.Credit)
                    {
                        Enqueue(new S.MarketFail { Reason = 4 });
                        return;
                    }

                    if (!CanGainItem(auction.Item))
                    {
                        Enqueue(new S.MarketFail { Reason = 5 });
                        return;
                    }

                    UserItem item = Envir.CreateFreshItem(auction.Item.Info);

                    Account.Credit -= auction.Price;
                    GainItem(item);
                    Enqueue(new S.MarketSuccess { Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.BoughtItemForCredit), auction.Item.FriendlyName, auction.Price) });
                    MarketSearch(MatchName, MatchType);

                    return;
                }
            }
            else
            {
                if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.MarketKey, StringComparison.CurrentCultureIgnoreCase))
                {
                    Enqueue(new S.MarketFail { Reason = 1 });
                    return;
                }

                for (int n = 0; n < CurrentMap.NPCs.Count; n++)
                {
                    NPCObject ob = CurrentMap.NPCs[n];
                    if (ob.ObjectID != NPCObjectID) continue;

                    if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                    foreach (AuctionInfo auction in Search)
                    {
                        if (auction.AuctionID != auctionID) continue;
                        if (auction.ItemType != MarketItemType.Consign && auction.ItemType != MarketItemType.Auction) continue;

                        if (auction.Sold)
                        {
                            Enqueue(new S.MarketFail { Reason = 2 });
                            return;
                        }

                        if (auction.Expired)
                        {
                            Enqueue(new S.MarketFail { Reason = 3 });
                            return;
                        }

                        if (!Envir.Auctions.Contains(auction))
                        {
                            Enqueue(new S.MarketFail { Reason = 3 });
                            return;
                        }

                        if (!CanGainItem(auction.Item))
                        {
                            Enqueue(new S.MarketFail { Reason = 5 });
                            return;
                        }

                        if (Account.Auctions.Contains(auction))
                        {
                            Enqueue(new S.MarketFail { Reason = 6 });
                            return;
                        }

                        if (auction.Price > Account.Gold || bidPrice > Account.Gold)
                        {
                            Enqueue(new S.MarketFail { Reason = 4 });
                            return;
                        }

                        if (auction.ItemType == MarketItemType.Consign)
                        {
                            auction.Sold = true;

                            Account.Gold -= auction.Price;

                            Enqueue(new S.LoseGold { Gold = auction.Price });
                            GainItem(auction.Item);

                            Envir.MessageAccount(auction.SellerInfo.AccountInfo, GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.SoldItemForGold), auction.Item.FriendlyName, auction.Price), ChatType.System);
                            Enqueue(new S.MarketSuccess { Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.BoughtItemForGold), auction.Item.FriendlyName, auction.Price) });
                            MarketSearch(MatchName, MatchType);
                        }
                        else
                        {
                            if (auction.CurrentBid > bidPrice)
                            {
                                Enqueue(new S.MarketFail { Reason = 9 });
                                return;
                            }

                            if (auction.CurrentBuyerInfo != null)
                            {
                                string message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.OutbidRefundGold), auction.Item.FriendlyName, auction.CurrentBid);

                                Envir.MailCharacter(auction.CurrentBuyerInfo, gold: auction.CurrentBid, customMessage: message);
                            }

                            auction.CurrentBid = bidPrice;
                            auction.CurrentBuyerIndex = Info.Index;
                            auction.CurrentBuyerInfo = Info;

                            Account.Gold -= bidPrice;
                            Enqueue(new S.LoseGold { Gold = bidPrice });

                            Envir.MessageAccount(auction.SellerInfo.AccountInfo, GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.SomeoneBidGoldForItem), auction.Item.FriendlyName, auction.CurrentBid), ChatType.System);
                            Enqueue(new S.MarketSuccess { Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouBidGoldForItem), auction.Item.FriendlyName, auction.CurrentBid) });
                            MarketSearch(MatchName, MatchType);
                        }

                        return;
                    }
                }
            }

            Enqueue(new S.MarketFail { Reason = 7 });
        }

        public void MarketSellNow(ulong auctionID)
        {
            if (Dead)
            {
                Enqueue(new S.MarketFail { Reason = 0 });
                return;
            }

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.MarketKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(new S.MarketFail { Reason = 1 });
                return;
            }

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCObjectID) continue;
                if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                foreach (AuctionInfo auction in Account.Auctions)
                {
                    if (auction.AuctionID != auctionID) continue;

                    if (auction.ItemType != MarketItemType.Auction)
                    {
                        return;
                    }

                    if (auction.CurrentBid <= auction.Price || auction.CurrentBuyerInfo == null)
                    {
                        Enqueue(new S.MarketFail { Reason = 9 });
                        return;
                    }

                    if (auction.Sold && auction.Expired)
                    {
                        MessageQueue.Enqueue(string.Format("Auction both sold and Expired {0}", Account.AccountID));
                        return;
                    }

                    if (auction.Expired || auction.Sold || Envir.Now >= auction.ConsignmentDate.AddDays(Globals.ConsignmentLength))
                    {
                        Enqueue(new S.MarketFail { Reason = 10 });
                        return;
                    }

                    uint cost = auction.CurrentBid;

                    uint gold = (uint)Math.Max(0, cost - cost * Globals.Commission);

                    if (!CanGainGold(auction.CurrentBid))
                    {
                        Enqueue(new S.MarketFail { Reason = 8 });
                        return;
                    }

                    auction.Sold = true;

                    string message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouWonItemForGold), auction.Item.FriendlyName, auction.CurrentBid);

                    Envir.MailCharacter(auction.CurrentBuyerInfo, item: auction.Item, customMessage: message);
                    Envir.MessageAccount(auction.CurrentBuyerInfo.AccountInfo, GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouBoughtItemForGold), auction.Item.FriendlyName, auction.CurrentBid), ChatType.System);

                    Account.Auctions.Remove(auction);
                    Envir.Auctions.Remove(auction);
                    GainGold(gold);
                    Enqueue(new S.MarketSuccess { Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouSoldItemGoldEarningsCommission), auction.Item.FriendlyName, cost, gold, cost - gold) });
                    MarketSearch(MatchName, MatchType);
                    return;
                }

            }

            Enqueue(new S.MarketFail { Reason = 7 });
        }

        public void MarketGetBack(MarketCollectionMode mode, ulong auctionID)
        {
            AuctionInfo GetAuction(ulong auctionID)
            {
                foreach (var auction in Account.Auctions)
                {
                    if (auction.AuctionID == auctionID)
                        return auction;
                }
                return null;
            }

            bool TakeAuction(AuctionInfo auction)
            {
                if (auction.Sold && auction.Expired)
                {
                    MessageQueue.Enqueue(string.Format("Auction both sold and Expired {0}", Account.AccountID));
                    return false;
                }

                if (mode == MarketCollectionMode.Any || (mode == MarketCollectionMode.Expired && auction.Expired))
                {
                    if (!auction.Sold || auction.Expired)
                    {
                        if (!CanGainItem(auction.Item))
                        {
                            Enqueue(new S.MarketFail { Reason = 5 });
                            return false;
                        }

                        if (auction.CurrentBuyerInfo != null)
                        {
                            string message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.AuctionOutbidRefund), auction.Item.FriendlyName, auction.CurrentBid);

                            Envir.MailCharacter(auction.CurrentBuyerInfo, gold: auction.CurrentBid, customMessage: message);
                        }

                        GainItem(auction.Item);
                        return true;
                    }
                }

                if (mode == MarketCollectionMode.Any || (mode == MarketCollectionMode.Sold && auction.Sold))
                {
                    uint cost = auction.ItemType == MarketItemType.Consign ? auction.Price : auction.CurrentBid;

                    if (!CanGainGold(cost))
                    {
                        Enqueue(new S.MarketFail { Reason = 8 });
                        return false;
                    }

                    uint gold = (uint)Math.Max(0, cost - cost * Globals.Commission);

                    GainGold(gold);
                    Enqueue(new S.MarketSuccess { Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.SoldItemEarningsCommission), auction.Item.FriendlyName, cost, gold, cost - gold) });
                    return true;
                }

                return false;
            }


            if (Dead)
            {
                Enqueue(new S.MarketFail { Reason = 0 });
                return;
            }

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.MarketKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(new S.MarketFail { Reason = 1 });
                return;
            }

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCObjectID) continue;
                if (!Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange)) return;

                if (mode == 0)
                {
                    var auction = GetAuction(auctionID);
                    if (auction != null)
                    {
                        if (TakeAuction(auction))
                        {
                            Account.Auctions.Remove(auction);
                            Envir.Auctions.Remove(auction);
                            MarketSearch(MatchName, MatchType);
                            return;
                        }
                    }
                }
                else
                {
                    int count = 0;
                    var node = Account.Auctions.First;
                    while (node != null)
                    {
                        var next = node.Next;

                        var auction = node.Value;
                        if (auction != null)
                        {
                            if (TakeAuction(auction))
                            {
                                Account.Auctions.Remove(node);
                                Envir.Auctions.Remove(auction);
                                count++;
                            }
                        }
                        node = next;
                    }

                    if (count > 0)
                    {
                        MarketSearch(MatchName, MatchType);
                        return;
                    }
                }
            }

            Enqueue(new S.MarketFail { Reason = 7 });
        }

        public void RequestUserName(uint id)
        {
            CharacterInfo Character = Envir.GetCharacterInfo((int)id);
            if (Character != null)
                Enqueue(new S.UserName { Id = (uint)Character.Index, Name = Character.Name });
        }

        #endregion
    }
}
