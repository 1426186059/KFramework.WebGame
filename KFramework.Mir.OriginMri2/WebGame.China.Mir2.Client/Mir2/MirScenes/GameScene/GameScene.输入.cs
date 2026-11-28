using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

namespace Client.MirScenes
{
    public sealed partial class GameScene : MirScene
    {
        private void GameScene_KeyDown(object sender, KeyEventArgs e)
        {
            // GM 工具面板: 组合键 Ctrl+G 开关（用当前事件的 e.Control，比 CMain.Ctrl 更准；
            // 但浏览器 WASM 下 Ctrl+G 可能被浏览器/系统拦截，左下角还有 GM 按钮兜底）
            if (e.Control && e.KeyCode == Keys.KeyG)
            {
                GMDialog.Toggle();
                return;
            }

            if (GameScene.Scene.KeyboardLayoutDialog.WaitingForBind != null)
            {
                GameScene.Scene.KeyboardLayoutDialog.CheckNewInput(e);
                return;
            }

            // 聊天输入框聚焦时不处理技能快捷键，数字键用于正常打字。
            if (ChatDialog != null && ChatDialog.ChatTextBox != null &&
                ChatDialog.ChatTextBox.TextBox != null && ChatDialog.ChatTextBox.TextBox.IsFocused)
                return;

            foreach (KeyBind KeyCheck in CMain.InputKeys.Keylist)
            {
                if (KeyCheck.Key == Keys.None)
                    continue;
                if (KeyCheck.Key != e.KeyCode)
                    continue;
                if (KeyCheck.Key == Keys.Insert && e.KeyCode == Keys.Insert && e.Control)
                {
                    //When Ctrl is held down, KeyData=Insert | Control, the system may mistakenly recognize it as Keys.Insert, requiring special judgment to be added.
                    continue;
                }
                if ((KeyCheck.RequireAlt != 2) && (KeyCheck.RequireAlt != (CMain.Alt ? 1 : 0)))
                    continue;
                if ((KeyCheck.RequireShift != 2) && (KeyCheck.RequireShift != (CMain.Shift ? 1 : 0)))
                    continue;
                if ((KeyCheck.RequireCtrl != 2) && (KeyCheck.RequireCtrl != (CMain.Ctrl ? 1 : 0)))
                    continue;
                if ((KeyCheck.RequireTilde != 2) && (KeyCheck.RequireTilde != (CMain.Tilde ? 1 : 0)))
                    continue;
                // 技能快捷键(数字1-8 / F1-F8 等)：KeyDown 已消费，抑制紧随的 KeyPress，
                // 否则数字键的字符会落到聊天输入框（表现为按技能键激活了聊天）。
                if (KeyCheck.function >= KeybindOptions.Bar1Skill1 && KeyCheck.function <= KeybindOptions.HeroSkill8)
                    CMain.SuppressNextKeyPress = true;

                //now run the real code
                switch (KeyCheck.function)
                {
                    case KeybindOptions.Bar1Skill1: UseSpell(1); break;
                    case KeybindOptions.Bar1Skill2: UseSpell(2); break;
                    case KeybindOptions.Bar1Skill3: UseSpell(3); break;
                    case KeybindOptions.Bar1Skill4: UseSpell(4); break;
                    case KeybindOptions.Bar1Skill5: UseSpell(5); break;
                    case KeybindOptions.Bar1Skill6: UseSpell(6); break;
                    case KeybindOptions.Bar1Skill7: UseSpell(7); break;
                    case KeybindOptions.Bar1Skill8: UseSpell(8); break;
                    case KeybindOptions.Bar2Skill1: UseSpell(9); break;
                    case KeybindOptions.Bar2Skill2: UseSpell(10); break;
                    case KeybindOptions.Bar2Skill3: UseSpell(11); break;
                    case KeybindOptions.Bar2Skill4: UseSpell(12); break;
                    case KeybindOptions.Bar2Skill5: UseSpell(13); break;
                    case KeybindOptions.Bar2Skill6: UseSpell(14); break;
                    case KeybindOptions.Bar2Skill7: UseSpell(15); break;
                    case KeybindOptions.Bar2Skill8: UseSpell(16); break;
                    case KeybindOptions.HeroSkill1: UseSpell(17); break;
                    case KeybindOptions.HeroSkill2: UseSpell(18); break;
                    case KeybindOptions.HeroSkill3: UseSpell(19); break;
                    case KeybindOptions.HeroSkill4: UseSpell(20); break;
                    case KeybindOptions.HeroSkill5: UseSpell(21); break;
                    case KeybindOptions.HeroSkill6: UseSpell(22); break;
                    case KeybindOptions.HeroSkill7: UseSpell(23); break;
                    case KeybindOptions.HeroSkill8: UseSpell(24); break;
                    case KeybindOptions.Inventory:
                    case KeybindOptions.Inventory2:
                        if (!InventoryDialog.Visible) InventoryDialog.Show();
                        else InventoryDialog.Hide();
                        break;
                    case KeybindOptions.Equipment:
                    case KeybindOptions.Equipment2:
                        if (!CharacterDialog.Visible || !CharacterDialog.CharacterPage.Visible)
                        {
                            CharacterDialog.Show();
                            CharacterDialog.ShowCharacterPage();
                        }
                        else CharacterDialog.Hide();
                        break;
                    case KeybindOptions.Skills:
                    case KeybindOptions.Skills2:
                        CharacterSkillDialog.Toggle();
                        break;
                    case KeybindOptions.HeroInventory:
                        if (Hero == null)
                            break;
                        if (!HeroInventoryDialog.Visible) HeroInventoryDialog.Show();
                        else HeroInventoryDialog.Hide();
                        break;
                    case KeybindOptions.HeroEquipment:
                        if (Hero == null)
                            break;
                        if (!HeroDialog.Visible || !HeroDialog.CharacterPage.Visible)
                        {
                            HeroDialog.Show();
                            HeroDialog.ShowCharacterPage();
                        }
                        else HeroDialog.Hide();
                        break;
                    case KeybindOptions.HeroSkills:
                        CharacterSkillDialog.Toggle();
                        break;
                    case KeybindOptions.Creature:
                        if (!IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Show();
                        else IntelligentCreatureDialog.Hide();
                        break;
                    case KeybindOptions.MountWindow:
                        if (!MountDialog.Visible) MountDialog.Show();
                        else MountDialog.Hide();
                        break;

                    case KeybindOptions.GameShop:
                        if (!GameShopDialog.Visible) GameShopDialog.Show();
                        else GameShopDialog.Hide();
                        break;
                    case KeybindOptions.Fishing:
                        if (!FishingDialog.Visible) FishingDialog.Show();
                        else FishingDialog.Hide();
                        break;
                    case KeybindOptions.Skillbar:
                        if (!Settings.SkillBar)
                            SkillBarDialogs.Show();
                        else
                            SkillBarDialogs.Hide();
                        break;
                    case KeybindOptions.Mount:
                        if (GameScene.Scene.MountDialog.CanRide())
                            GameScene.Scene.MountDialog.Ride();
                        break;
                    case KeybindOptions.Mentor:
                        if (!MentorDialog.Visible) MentorDialog.Show();
                        else MentorDialog.Hide();
                        break;
                    case KeybindOptions.Relationship:
                        if (!RelationshipDialog.Visible) RelationshipDialog.Show();
                        else RelationshipDialog.Hide();
                        break;
                    case KeybindOptions.Friends:
                        if (!FriendDialog.Visible) FriendDialog.Show();
                        else FriendDialog.Hide();
                        break;
                    case KeybindOptions.Guilds:
                        if (!GuildDialog.Visible) GuildDialog.Show();
                        else
                        {
                            GuildDialog.Hide();
                        }
                        break;

                    case KeybindOptions.Ranking:
                        if (!RankingDialog.Visible) RankingDialog.Show();
                        else RankingDialog.Hide();
                        break;
                    case KeybindOptions.Quests:
                        if (!QuestLogDialog.Visible) QuestLogDialog.Show();
                        else QuestLogDialog.Hide();
                        break;
                    case KeybindOptions.Exit:
                        QuitGame();
                        return;

                    case KeybindOptions.Closeall:
                        InventoryDialog.Hide();
                        CharacterDialog.Hide();
                        CharacterSkillDialog.Hide();
                        OptionDialog.Hide();
                        MenuDialog.Hide();
                        if (NPCDialog.Visible) NPCDialog.Hide();
                        HelpDialog.Hide();
                        KeyboardLayoutDialog.Hide();
                        RankingDialog.Hide();
                        IntelligentCreatureDialog.Hide();
                        IntelligentCreatureOptionsDialog.Hide();
                        IntelligentCreatureOptionsGradeDialog.Hide();
                        MountDialog.Hide();
                        FishingDialog.Hide();
                        FriendDialog.Hide();
                        RelationshipDialog.Hide();
                        MentorDialog.Hide();
                        GameShopDialog.Hide();
                        GroupDialog.Hide();
                        GuildDialog.Hide();
                        InspectDialog.Hide();
                        StorageDialog.Hide();
                        TrustMerchantDialog.Hide();
                        QuestListDialog.Hide();
                        QuestDetailDialog.Hide();
                        QuestLogDialog.Hide();
                        NPCAwakeDialog.Hide();
                        RefineDialog.Hide();
                        BigMapDialog.Hide();
                        if (FishingStatusDialog.bEscExit) FishingStatusDialog.Cancel();
                        MailComposeLetterDialog.Hide();
                        MailComposeParcelDialog.Hide();
                        MailListDialog.Hide();
                        MailReadLetterDialog.Hide();
                        MailReadParcelDialog.Hide();
                        ItemRentalDialog.Hide();
                        WelcomeNoticeDialog.Hide();
                        HeroInventoryDialog?.Hide();
                        HeroManageDialog?.Hide();
                        HeroDialog?.Hide();

                        GameScene.Scene.DisposeItemLabel();
                        break;
                    case KeybindOptions.Options:
                    case KeybindOptions.Options2:
                        if (!OptionDialog.Visible) OptionDialog.Show();
                        else OptionDialog.Hide();
                        break;
                    case KeybindOptions.Group:
                        if (!GroupDialog.Visible) GroupDialog.Show();
                        else GroupDialog.Hide();
                        break;
                    case KeybindOptions.Belt:
                        if (!BeltDialog.Visible) BeltDialog.Show();
                        else BeltDialog.Hide();
                        break;
                    case KeybindOptions.BeltFlip:
                        //BeltDialog.Flip();
                        break;
                    case KeybindOptions.Pickup:
                        if (CMain.Time > PickUpTime)
                        {
                            PickUpTime = CMain.Time + 200;
                            Network.Enqueue(new C.PickUp());
                        }
                        break;
                    case KeybindOptions.Belt1:
                    case KeybindOptions.Belt1Alt:
                        BeltDialog.Grid[0].UseItem();
                        break;
                    case KeybindOptions.Belt2:
                    case KeybindOptions.Belt2Alt:
                        BeltDialog.Grid[1].UseItem();
                        break;
                    case KeybindOptions.Belt3:
                    case KeybindOptions.Belt3Alt:
                        BeltDialog.Grid[2].UseItem();
                        break;
                    case KeybindOptions.Belt4:
                    case KeybindOptions.Belt4Alt:
                        BeltDialog.Grid[3].UseItem();
                        break;
                    case KeybindOptions.Belt5:
                    case KeybindOptions.Belt5Alt:
                        BeltDialog.Grid[4].UseItem();
                        break;
                    case KeybindOptions.Belt7:
                    case KeybindOptions.Belt7Alt:
                        HeroBeltDialog?.Grid[0].UseItem();
                        break;
                    case KeybindOptions.Belt8:
                    case KeybindOptions.Belt8Alt:
                        HeroBeltDialog?.Grid[1].UseItem();
                        break;
                    case KeybindOptions.Logout:
                        LogOut();
                        break;
                    case KeybindOptions.Minimap:
                        MiniMapDialog.Toggle();
                        break;
                    case KeybindOptions.Bigmap:
                        BigMapDialog.Toggle();
                        break;
                    case KeybindOptions.Trade:
                        Network.Enqueue(new C.TradeRequest());
                        break;
                    case KeybindOptions.Rental:
                        ItemRentalDialog.Toggle();
                        break;
                    case KeybindOptions.ChangePetmode:
                        ChangePetMode();
                        break;
                    case KeybindOptions.PetmodeBoth:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.Both });
                        return;
                    case KeybindOptions.PetmodeMoveonly:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.MoveOnly });
                        return;
                    case KeybindOptions.PetmodeAttackonly:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.AttackOnly });
                        return;
                    case KeybindOptions.PetmodeNone:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.None });
                        return;
                    case KeybindOptions.PetmodeFocusMasterTarget:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.FocusMasterTarget });
                        return;
                    case KeybindOptions.CreatureAutoPickup://semiauto!
                        if (CMain.Time > IntelligentCreaturePickupTime)
                        {
                            IntelligentCreaturePickupTime = CMain.Time + 200;
                            Network.Enqueue(new C.IntelligentCreaturePickup { MouseMode = false, Location = MapControl.MapLocation });
                        }
                        break;
                    case KeybindOptions.CreaturePickup:
                        if (CMain.Time > IntelligentCreaturePickupTime)
                        {
                            IntelligentCreaturePickupTime = CMain.Time + 200;
                            Network.Enqueue(new C.IntelligentCreaturePickup { MouseMode = true, Location = MapControl.MapLocation });
                        }
                        break;
                    case KeybindOptions.ChangeAttackmode:
                        ChangeAttackMode();
                        break;
                    case KeybindOptions.AttackmodePeace:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Peace });
                        return;
                    case KeybindOptions.AttackmodeGroup:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Group });
                        return;
                    case KeybindOptions.AttackmodeGuild:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Guild });
                        return;
                    case KeybindOptions.AttackmodeEnemyguild:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.EnemyGuild });
                        return;
                    case KeybindOptions.AttackmodeRedbrown:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.RedBrown });
                        return;
                    case KeybindOptions.AttackmodeAll:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.All });
                        return;

                    case KeybindOptions.Help:
                        if (!HelpDialog.Visible) HelpDialog.Show();
                        else HelpDialog.Hide();
                        break;
                    case KeybindOptions.Keybind:
                        if (!KeyboardLayoutDialog.Visible) KeyboardLayoutDialog.Show();
                        else KeyboardLayoutDialog.Hide();
                        break;
                    case KeybindOptions.Autorun:
                        MapControl.AutoRun = !MapControl.AutoRun;
                        break;
                    case KeybindOptions.Cameramode:

                        if (!MainDialog.Visible)
                        {
                            MainDialog.Show();
                            ChatDialog.Show();
                            BeltDialog.Show();
                            MiniMapDialog.Show();
                            BuffsDialog.Show();
                        }
                        else
                        {
                            MainDialog.Hide();
                            ChatDialog.Hide();
                            BeltDialog.Hide();
                            MiniMapDialog.Hide();
                            BuffsDialog.Hide();
                        }
                        break;
                    case KeybindOptions.DropView:
                        if (CMain.Time > DropViewTime)
                            DropViewTime = CMain.Time + 5000;
                        break;
                    case KeybindOptions.TargetDead:
                        if (CMain.Time > TargetDeadTime)
                            TargetDeadTime = CMain.Time + 5000;
                        break;
                    case KeybindOptions.AddGroupMember:
                        if (MapObject.MouseObject == null) break;
                        if (MapObject.MouseObject.Race != ObjectType.Player) break;

                        GameScene.Scene.GroupDialog.AddMember(MapObject.MouseObject.Name);
                        break;
                }
            }
        }

        public void ChangePetMode()
        {
            if (CMain.Time > ChangePModeTime)
            {
                ChangePModeTime = CMain.Time + 500;

                switch (PMode)
                {
                    case PetMode.Both:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.MoveOnly });
                        return;
                    case PetMode.MoveOnly:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.AttackOnly });
                        return;
                    case PetMode.AttackOnly:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.None });
                        return;
                    case PetMode.None:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.FocusMasterTarget });
                        return;
                    case PetMode.FocusMasterTarget:
                        Network.Enqueue(new C.ChangePMode { Mode = PetMode.Both });
                        return;
                }
            }
        }

        public void ChangeAttackMode()
        {
            if (CMain.Time > ChangeAModeTime)
            {
                ChangeAModeTime = CMain.Time + 300;

                switch (AMode)
                {
                    case AttackMode.Peace:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Group });
                        return;
                    case AttackMode.Group:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Guild });
                        return;
                    case AttackMode.Guild:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.EnemyGuild });
                        return;
                    case AttackMode.EnemyGuild:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.RedBrown });
                        return;
                    case AttackMode.RedBrown:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.All });
                        return;
                    case AttackMode.All:
                        Network.Enqueue(new C.ChangeAMode { Mode = AttackMode.Peace });
                        return;
                }
            }
        }

        private void SendSpellToggle(UserObject Actor, Spell Spell, bool CanUse)
        {
            if (Actor == User)
                Network.Enqueue(new C.SpellToggle { Spell = Spell, CanUse = CanUse });
            else
                Network.Enqueue(new C.SpellToggle { Spell = Spell });
        }
        public void QuitGame()
        {
            // Quit = 退出到选人界面，不受战斗 LogTime 限制；确认后由服务端 LogOutSuccess 切场景。
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("ExitTip"), MirMessageBoxButtons.YesNo);
            messageBox.YesButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.LogOut());
                Enabled = false;
            };
            messageBox.Show();
        }
        public void LogOut()
        {
            // 返回选人界面同样直接弹确认框，不受战斗 LogTime 限制。
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("LogOutTip"), MirMessageBoxButtons.YesNo);
            messageBox.YesButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.LogOut());
                Enabled = false;
            };
            messageBox.Show();
        }

        protected internal override void DrawControl()
        {
            if (MapControl != null && !MapControl.IsDisposed)
            {
                MapControl.DrawControl();
            }

            base.DrawControl();

            if (PickedUpGold || (SelectedCell != null && SelectedCell.Item != null))
            {
                if (PickedUpGold)
                {
                    int image = 116;
                    Size imgSize = Libraries.Items.GetTrueSize(image);
                    Point p = CMain.MPoint.Add(-imgSize.Width / 2, -imgSize.Height / 2);

                    if (p.X + imgSize.Width >= DXManager.GDevice.Viewport.Width)
                        p.X = DXManager.GDevice.Viewport.Width - imgSize.Width;

                    if (p.Y + imgSize.Height >= DXManager.GDevice.Viewport.Height)
                        p.Y = DXManager.GDevice.Viewport.Height - imgSize.Height;

                    Libraries.Items.Draw(image, p.X, p.Y);
                }
                else
                {
                    // 拖拽预览走 origin 图标（与 MirItemCell.DrawItemIcon 同链路）：
                    // 服务器 S.NewItemInfo 下发的 ItemInfoTable.Image 已是完整 uint，直接用。
                    var item = SelectedCell.Item;
                    int id = (int)item.Info.Image;
                    if (id > 0)
                    {
                        const int box = 48;
                        Point p = CMain.MPoint.Add(-box / 2, -box / 2);
                        Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Item, id,
                                                        priority: EResLoadPriority.User);
                        Mir2OriginObjectLibrary.DrawItemIconFit(id, p.X, p.Y, box, box);
                    }
                }
            }

            for (int i = 0; i < OutputLines.Length; i++)
                OutputLines[i].Draw();
        }

        public override void Process()
        {
            if (MapControl == null || User == null)
            {
                return;
            }

            // 技能键按住不放：按 100ms 节流持续尝试，真正的 CD 判断仍在 UseSpell 内部。
            foreach (int slot in CMain.HeldSkillSlots)
            {
                if (slot <= 0 || slot >= _holdSkillNextTry.Length)
                {
                    continue;
                }

                if (CMain.Time < _holdSkillNextTry[slot])
                {
                    continue;
                }

                _holdSkillNextTry[slot] = CMain.Time + 100;
                UseSpell(slot, true);
            }

            if (CMain.Time >= MoveTime)
            {
                MoveTime = CMain.Time + 100; //Move Speed
                CanMove = true;
                MapControl.AnimationCount++;
                MapControl.TextureValid = false;
            }
            else
                CanMove = false;

            if (CMain.Time >= CMain.NextPing)
            {
                CMain.NextPing = CMain.Time + 60000;
                Network.Enqueue(new C.KeepAlive() { Time = CMain.Time });
            }

            TimerControl.Process();
            CompassControl.Process();
            RankingDialog.Process();
            QuickActionDialog.Process(CMain.Time);

            MirItemCell cell = MouseControl as MirItemCell;

            if (cell != null && HoverItem != cell.Item && HoverItem != cell.ShadowItem)
            {
                DisposeItemLabel();
                HoverItem = null;
                CreateItemLabel(cell.Item);
            }

            if (ItemLabel != null && !ItemLabel.IsDisposed)
            {
                ItemLabel.BringToFront();

                int x = CMain.MPoint.X + 28, y = CMain.MPoint.Y + 28;
                if (x + ItemLabel.Size.Width > DXManager.GDevice.Viewport.Width)
                    x = DXManager.GDevice.Viewport.Width - ItemLabel.Size.Width;

                if (y + ItemLabel.Size.Height > DXManager.GDevice.Viewport.Height)
                    y = DXManager.GDevice.Viewport.Height - ItemLabel.Size.Height;
                ItemLabel.Location = new Point(x, y);
            }

            if (MailLabel != null && !MailLabel.IsDisposed)
            {
                MailLabel.BringToFront();

                int x = CMain.MPoint.X + 15, y = CMain.MPoint.Y;
                if (x + MailLabel.Size.Width > DXManager.GDevice.Viewport.Width)
                    x = DXManager.GDevice.Viewport.Width - MailLabel.Size.Width;

                if (y + MailLabel.Size.Height > DXManager.GDevice.Viewport.Height)
                    y = DXManager.GDevice.Viewport.Height - MailLabel.Size.Height;
                MailLabel.Location = new Point(x, y);
            }

            if (MemoLabel != null && !MemoLabel.IsDisposed)
            {
                MemoLabel.BringToFront();

                int x = CMain.MPoint.X + 15, y = CMain.MPoint.Y;
                if (x + MemoLabel.Size.Width > DXManager.GDevice.Viewport.Width)
                    x = DXManager.GDevice.Viewport.Width - MemoLabel.Size.Width;

                if (y + MemoLabel.Size.Height > DXManager.GDevice.Viewport.Height)
                    y = DXManager.GDevice.Viewport.Height - MemoLabel.Size.Height;
                MemoLabel.Location = new Point(x, y);
            }

            if (GuildBuffLabel != null && !GuildBuffLabel.IsDisposed)
            {
                GuildBuffLabel.BringToFront();

                int x = CMain.MPoint.X + 15, y = CMain.MPoint.Y;
                if (x + GuildBuffLabel.Size.Width > DXManager.GDevice.Viewport.Width)
                    x = DXManager.GDevice.Viewport.Width - GuildBuffLabel.Size.Width;

                if (y + GuildBuffLabel.Size.Height > DXManager.GDevice.Viewport.Height)
                    y = DXManager.GDevice.Viewport.Height - GuildBuffLabel.Size.Height;
                GuildBuffLabel.Location = new Point(x, y);
            }

            if (!User.Dead) ShowReviveMessage = false;

            if (ShowReviveMessage && CMain.Time > User.DeadTime && User.CurrentAction == MirAction.Dead)
            {
                ShowReviveMessage = false;
                MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("DiedTip"), MirMessageBoxButtons.YesNo, false);

                messageBox.YesButton.Click += (o, e) =>
                {
                    if (User.Dead) Network.Enqueue(new C.TownRevive());
                };

                messageBox.AfterDraw += (o, e) =>
                {
                    if (!User.Dead) messageBox.Dispose();
                };

                messageBox.Show();
            }

            BuffsDialog.Process();

            MapControl.Process();
            MainDialog.Process();
            InventoryDialog.Process();
            GameShopDialog.Process();
            MiniMapDialog.Process();

            Scene.SkillBarDialogs.Process();

            DialogProcess();

            ProcessOuput();

            UpdateMouseCursor();

            SoundManager.ProcessDelayedSounds();

            _targetHealth?.RefreshTarget();
        }

        public void DialogProcess()
        {
            Scene.SkillBarDialogs.Show();
        }

    }
}