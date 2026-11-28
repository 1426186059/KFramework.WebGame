using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using SlimDX;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Effect = Client.MirObjects.Effect;
using S = ServerPackets;

namespace Client.MirScenes
{
    public sealed partial class GameScene : MirScene
    {
        private void ObjectNPC(S.ObjectNPC p)
        {
            NPCObject ob = new NPCObject(p.ObjectID);
            ob.Load(p);
        }
        private void NPCResponse(S.NPCResponse p)
        {
            NPCTime = 0;
            NPCDialog.BigButtons.Clear();
            NPCDialog.BigButtonDialog.Hide();
            if (NPCTeleporterDialog.SuppressNpcDialog)
            {
                // 自定义传送对话框模式：发 @Main 仅为在服务端“开起”与传送员的对话（设置 NPCObjectID/NPCScriptID），
                // 其回包（原菜单文本）须忽略，否则会弹出原始 NPC 菜单并覆盖自定义框。
                NPCTeleporterDialog.SuppressNpcDialog = false;
                return;
            }
            NPCTeleporterDialog.Hide();

            // 打开 NPC 对话/任务面板时停止自动战斗与自动寻路，避免玩家在看对话/任务时被自动拉走或继续打怪。
            MapControl.AutoFight = false;
            MapControl.AutoPath = false;

            NPCDialog.NewText(p.Page);

            // NPC 有可接/可交任务：直接弹任务面板，不再弹 NPC 菜单（省得玩家多一步点击）
            bool npcHasQuest = false;
            try
            {
                var npcForQuest = NPCID != 0 ? MapControl.GetObject(NPCID) as NPCObject : null;
                if (npcForQuest != null && npcForQuest.GetAvailableQuests().Count > 0)
                    npcHasQuest = true;
            }
            catch { npcHasQuest = false; }

            // 原始传奇 checknpcTaskDialog：服务端判定任务待接/待交且点的是该任务 NPC 时，
            // 直接弹 NPC 任务面板，不再看 NPC 有没有对话/商店内容。
            // 有对话/商店脚本（书店老板等）则优先普通 NPC 面板，让玩家能直接购买。
            bool hasNpcContent = p.Page.Count > 0 || NPCDialog.BigButtons.Count > 0;
            // 服务端已按 checknpcTaskDialog 判定该弹任务面板时直接弹；不再被本地 npcHasQuest 挡掉
            // （商店 NPC 打开购买面板后本地任务绑定可能尚未刷新，导致买了药任务推进却不切面板）。
            bool showQuestPanel = p.OpenQuestPanel || (npcHasQuest && !hasNpcContent);
            KFramework.MonoGame.PrintTool.Log($"[任务面板诊断] NPCID={NPCID} Open={p.OpenQuestPanel} 本地有任务={npcHasQuest} 页面内容={hasNpcContent} 显示任务面板={showQuestPanel}");
            if (showQuestPanel)
            {
                NPCDialog.Hide();
                if (p.OpenQuestPanel)
                    QuestListDialog.ShowForced(NPCID);
                else
                    QuestListDialog.Show();
            }
            else if (hasNpcContent)
            {
                NPCDialog.Show();
            }
            else
            {
                NPCDialog.Hide();
            }

            NPCGoodsDialog.Hide();
            NPCSubGoodsDialog.Hide();
            NPCCraftGoodsDialog.Hide();
            NPCDropDialog.Hide();
            StorageDialog.Hide();
            NPCAwakeDialog.Hide();
            RefineDialog.Hide();
            StorageDialog.Hide();
            TrustMerchantDialog.Hide();
            // 显示普通 NPC 面板时收起任务面板，否则两层面板叠加。
            if (!showQuestPanel)
                QuestListDialog.Hide();
        }

        private void NPCUpdate(S.NPCUpdate p)
        {
            GameScene.NPCID = p.NPCID; //Updates the client with the correct NPC ID if it's manually called from the client
        }

        private void NPCImageUpdate(S.NPCImageUpdate p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob) && ob is NPCObject npc && ob.Race == ObjectType.Merchant)
            {
                npc.Image = p.Image;
                npc.Colour = p.Colour;
                npc.LoadLibrary();
            }
        }

        private void DefaultNPC(S.DefaultNPC p)
        {
            GameScene.DefaultNPCID = p.ObjectID; //Updates the client with the correct Default NPC ID
        }


        private void ObjectHide(S.ObjectHide p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Hide, Direction = ob.Direction, Location = ob.CurrentLocation });
        }

        private void ObjectShow(S.ObjectShow p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Show, Direction = ob.Direction, Location = ob.CurrentLocation });
        }

        private void Poisoned(S.Poisoned p)
        {
            var previousPoisons = User.Poison;

            User.Poison = p.Poison;
            if (p.Poison.HasFlag(PoisonType.Stun) || p.Poison.HasFlag(PoisonType.Dazed) || p.Poison.HasFlag(PoisonType.Frozen) || p.Poison.HasFlag(PoisonType.Paralysis) || p.Poison.HasFlag(PoisonType.LRParalysis))
            {
                User.ClearMagic();
            }

            if (previousPoisons.HasFlag(PoisonType.Blindness) && !User.Poison.HasFlag(PoisonType.Blindness))
            {
                User.BlindCount = 0;
            }
        }

        private void ObjectPoisoned(S.ObjectPoisoned p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.Poison = p.Poison;
        }

        private void ObjectTeleportOut(S.ObjectTeleportOut p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                Effect effect = null;

                bool playDefaultSound = true;

                switch (p.Type)
                {
                    case 1: //Yimoogi
                        {
                            effect = new Effect(Libraries.Magic2, 1300, 10, 500, ob.CurrentLocation);
                            break;
                        }
                    case 2: //RedFoxman
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.RedFoxman], 243, 10, 500, ob.CurrentLocation);
                            break;
                        }
                    case 4: //MutatedManWorm
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.MutatedManworm], 272, 6, 500, ob.CurrentLocation);

                            SoundManager.PlaySound(((ushort)Monster.MutatedManworm) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 5: //WitchDoctor
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.WitchDoctor], 328, 20, 1000, ob.CurrentLocation);
                            SoundManager.PlaySound(((ushort)Monster.WitchDoctor) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 6: //TurtleKing
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.TurtleKing], 946, 10, 500, ob.CurrentLocation);
                            break;
                        }
                    case 7: //Mandrill
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.Mandrill], 280, 10, 1000, ob.CurrentLocation);
                            SoundManager.PlaySound(((ushort)Monster.Mandrill) * 10 + 6);
                            playDefaultSound = false;
                            break;
                        }
                    case 8: //DarkCaptain
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.DarkCaptain], 1224, 10, 1000, ob.CurrentLocation));
                            SoundManager.PlaySound(((ushort)Monster.DarkCaptain) * 10 + 8);
                            playDefaultSound = false;
                            break;
                        }
                    case 9: //Doe
                        {
                            effect = new Effect(Libraries.Monsters[(ushort)Monster.Doe], 208, 10, 1000, ob.CurrentLocation);
                            SoundManager.PlaySound(((ushort)Monster.Doe) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 10: //HornedCommander
                        {
                            MapControl.Effects.Add(effect = new Effect(Libraries.Monsters[(ushort)Monster.HornedCommander], 928, 10, 1000, ob.CurrentLocation));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    case 11: //SnowWolfKing
                        {
                            MapControl.Effects.Add(effect = new Effect(Libraries.Monsters[(ushort)Monster.SnowWolfKing], 561, 10, 1000, ob.CurrentLocation));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    default:
                        {
                            effect = new Effect(Libraries.Magic, 250, 10, 500, ob.CurrentLocation);
                            break;
                        }
                }

                MapControl.Effects.Add(effect);

                if (playDefaultSound)
                    SoundManager.PlaySound(SoundList.Teleport);
            }
        }
        private void ObjectTeleportIn(S.ObjectTeleportIn p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                bool playDefaultSound = true;

                switch (p.Type)
                {
                    case 1: //Yimoogi
                        {
                            ob.Effects.Add(new Effect(Libraries.Magic2, 1310, 10, 500, ob));
                            break;
                        }
                    case 2: //RedFoxman
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.RedFoxman], 253, 10, 500, ob));
                            break;
                        }
                    case 4: //MutatedManWorm
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.MutatedManworm], 278, 7, 500, ob));
                            SoundManager.PlaySound(((ushort)Monster.MutatedManworm) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 5: //WitchDoctor
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.WitchDoctor], 348, 20, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.WitchDoctor) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 6: //TurtleKing
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TurtleKing], 956, 10, 500, ob));
                            break;
                        }
                    case 7: //Mandrill
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mandrill], 290, 10, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.Mandrill) * 10 + 6);
                            playDefaultSound = false;
                            break;
                        }
                    case 8: //DarkCaptain
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.DarkCaptain], 1224, 10, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.DarkCaptain) * 10 + 9);
                            playDefaultSound = false;
                            break;
                        }
                    case 9: //Doe
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Doe], 208, 10, 1000, ob));
                            SoundManager.PlaySound(((ushort)Monster.Doe) * 10 + 7);
                            playDefaultSound = false;
                            break;
                        }
                    case 10: //HornedCommander
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HornedCommander], 928, 10, 1000, ob));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    case 11: //SnowWolfKing
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.SnowWolfKing], 571, 10, 1000, ob));
                            SoundManager.PlaySound(8455);
                            playDefaultSound = false;
                            break;
                        }
                    default:
                        {
                            ob.Effects.Add(new Effect(Libraries.Magic, 260, 10, 500, ob));
                            break;
                        }
                }

                if (p.ObjectID == User.ObjectID)
                    User.TargetID = User.LastTargetObjectId;

                if (playDefaultSound)
                    SoundManager.PlaySound(SoundList.Teleport);
            }
        }

        private void NPCGoods(S.NPCGoods p)
        {
            // 收到商店商品包说明服务端已确认商店请求，清掉购买链接的节流，保证关闭后可再次打开
            NPCTime = 0;
            KFramework.MonoGame.PrintTool.Log($"[商店诊断] 收到NPCGoods 商品数={p.List.Count} Type={p.Type} NPCDialog.Visible={NPCDialog.Visible}");
            for (int i = 0; i < p.List.Count; i++)
            {
                p.List[i].Info = GetItemInfo(p.List[i].ItemIndex);
                // 原始传奇商店价格：从包里的并行数组填入
                if (i < p.CurrencyTypes.Count)
                {
                    p.List[i].ShopCurrencyType = p.CurrencyTypes[i];
                    p.List[i].ShopPrice = p.Prices[i];
                }
            }

            NPCRate = p.Rate;
            NPCPanelType = p.Type;
            HideAddedStoreStats = p.HideAddedStats;

            if (!NPCDialog.Visible) return;

            switch (NPCPanelType)
            {
                case PanelType.Buy:
                    NPCGoodsDialog.UsePearls = false;
                    NPCGoodsDialog.NewGoods(p.List);
                    NPCGoodsDialog.Show();
                    break;
                case PanelType.BuySub:
                    NPCSubGoodsDialog.UsePearls = false;
                    NPCSubGoodsDialog.NewGoods(p.List);
                    NPCSubGoodsDialog.Show();
                    break;
                case PanelType.Craft:
                    NPCCraftGoodsDialog.UsePearls = false;
                    NPCCraftGoodsDialog.NewGoods(p.List);
                    NPCCraftGoodsDialog.Show();
                    CraftDialog.Show();
                    break;
            }
        }
        private void NPCPearlGoods(S.NPCPearlGoods p)
        {
            for (int i = 0; i < p.List.Count; i++)
            {
                p.List[i].Info = GetItemInfo(p.List[i].ItemIndex);
            }

            NPCRate = p.Rate;
            NPCPanelType = p.Type;

            if (!NPCDialog.Visible) return;

            NPCGoodsDialog.UsePearls = true;
            NPCGoodsDialog.NewGoods(p.List);
            NPCGoodsDialog.Show();
        }

        private void NPCSell()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Sell;
            NPCDropDialog.Show();
        }
        private void NPCRepair(S.NPCRepair p)
        {
            NPCRate = p.Rate;
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Repair;
            NPCDropDialog.Show();
        }
        private void NPCStorage()
        {
            if (NPCDialog.Visible)
                StorageDialog.Show();
        }
        private void StorageUnlockResult(S.StorageUnlockResult p)
        {
            StorageDialog?.HandleStorageUnlockResult(p);
        }
        private void StoragePasswordResult(S.StoragePasswordResult p)
        {
            StorageDialog?.HandleStoragePasswordResult(p);
        }
        private void NPCRequestInput(S.NPCRequestInput p)
        {
            MirInputBox inputBox = new MirInputBox(LanguageMgr.Get("PleaseEnterRequiredInformation"));

            inputBox.OKButton.Click += (o1, e1) =>
            {
                Network.Enqueue(new C.NPCConfirmInput { Value = inputBox.InputTextBox.Text, NPCID = p.NPCID, PageName = p.PageName });
                inputBox.Dispose();
            };
            inputBox.Show();
        }

        private void NPCSRepair(S.NPCSRepair p)
        {
            NPCRate = p.Rate;
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.SpecialRepair;
            NPCDropDialog.Show();
        }

        private void NPCRefine(S.NPCRefine p)
        {
            NPCRate = p.Rate;
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Refine;
            if (p.Refining)
            {
                NPCDropDialog.Hide();
                NPCDialog.Hide();
            }
            else
                NPCDropDialog.Show();
        }

        private void NPCCheckRefine(S.NPCCheckRefine p)
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.CheckRefine;
            NPCDropDialog.Show();
        }

        private void NPCCollectRefine(S.NPCCollectRefine p)
        {
            if (!NPCDialog.Visible) return;
            NPCDialog.Hide();
        }

        private void NPCReplaceWedRing(S.NPCReplaceWedRing p)
        {
            if (!NPCDialog.Visible) return;
            NPCRate = p.Rate;
            NPCDropDialog.PType = PanelType.ReplaceWedRing;
            NPCDropDialog.Show();
        }


        private void SellItem(S.SellItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);

            if (cell == null) return;

            cell.Locked = false;

            if (!p.Success) return;

            if (p.Count == cell.Item.Count)
                cell.Item = null;
            else
                cell.Item.Count -= p.Count;

            User.RefreshStats();
        }
        private void RepairItem(S.RepairItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);

            if (cell == null) return;

            cell.Locked = false;
        }
        private void CraftItem(S.CraftItem p)
        {
            if (!p.Success) return;

            CraftDialog.UpdateCraftCells();
            User.RefreshStats();
        }
        private void ItemRepaired(S.ItemRepaired p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                }
            }

            if (Hero != null)
            {
                if (item == null)
                {
                    for (int i = 0; i < Hero.Inventory.Length; i++)
                    {
                        if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Inventory[i];
                            break;
                        }
                    }
                }

                if (item == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Equipment[i];
                            break;
                        }
                    }
                }
            }

            if (item == null) return;

            item.MaxDura = p.MaxDura;
            item.CurrentDura = p.CurrentDura;

            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }
        }

        private void ItemSlotSizeChanged(S.ItemSlotSizeChanged p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                }
            }

            if (Hero != null)
            {
                if (item == null)
                {
                    for (int i = 0; i < Hero.Inventory.Length; i++)
                    {
                        if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Inventory[i];
                            break;
                        }
                    }
                }

                if (item == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Equipment[i];
                            break;
                        }
                    }
                }
            }

            if (item == null) return;

            item.SetSlotSize(p.SlotSize);
        }

        private void ItemSealChanged(S.ItemSealChanged p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                }
            }

            if (Hero != null)
            {
                if (item == null)
                {
                    for (int i = 0; i < Hero.Inventory.Length; i++)
                    {
                        if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Inventory[i];
                            break;
                        }
                    }
                }

                if (item == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.UniqueID)
                        {
                            item = Hero.Equipment[i];
                            break;
                        }
                    }
                }
            }

            if (item == null) return;

            item.SealedInfo = new SealedInfo { ExpiryDate = p.ExpiryDate };

            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }
        }

        private void ItemUpgraded(S.ItemUpgraded p)
        {
            UserItem item = null;
            MirGridType grid = MirGridType.Inventory;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.Item.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }
            }

            if (item == null && Hero != null)
            {
                for (int i = 0; i < Hero.Inventory.Length; i++)
                {
                    if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.Item.UniqueID)
                    {
                        item = Hero.Inventory[i];
                        grid = MirGridType.HeroInventory;
                        break;
                    }
                }
            }

            if (item == null) return;

            item.AddedStats.Clear();
            item.AddedStats.Add(p.Item.AddedStats);

            item.MaxDura = p.Item.MaxDura;
            item.RefineAdded = p.Item.RefineAdded;

            switch (grid)
            {
                case MirGridType.Inventory:
                    InventoryDialog.DisplayItemGridEffect(item.UniqueID, 0);
                    break;
                case MirGridType.HeroInventory:
                    HeroInventoryDialog.DisplayItemGridEffect(item.UniqueID, 0);
                    break;
            }


            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }
        }

    }
}