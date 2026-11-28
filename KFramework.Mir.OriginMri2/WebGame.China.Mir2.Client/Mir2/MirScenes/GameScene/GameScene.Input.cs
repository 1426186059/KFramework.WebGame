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

        public void UseSpell(int key, bool fromHold = false)
        {
            if (key > 0 && key < _holdSkillNextTry.Length)
            {
                _holdSkillNextTry[key] = CMain.Time + 100;
            }

            UserObject actor = User;
            if (key > 16)
            {
                if (HeroObject == null || CMain.Time < HeroSpellTime) return;

                actor = Hero;
                HeroSpellTime = CMain.Time + 200;
            }

            if (actor.Dead || actor.RidingMount || actor.Fishing) return;

            if (!actor.HasClassWeapon && actor.Weapon >= 0)
            {
                ChatDialog.ReceiveChat(LanguageMgr.Get("MustWearSuitableWeapon"), ChatType.System);
                return;
            }

            if (CMain.Time < actor.BlizzardStopTime || CMain.Time < actor.ReincarnationStopTime) return;

            ClientMagic magic = null;

            for (int i = 0; i < actor.Magics.Count; i++)
            {
                if (actor.Magics[i].Key != key) continue;
                magic = actor.Magics[i];
                break;
            }

            if (magic == null) return;

            switch (magic.Spell)
            {
                case Spell.反击: // 反击
                    if ((CMain.Time < magic.CastTime + magic.Delay))
                    {
                        if (CMain.Time >= OutputDelay)
                        {
                            OutputDelay = CMain.Time + 1000;
                            Scene.OutputMessage(LanguageMgr.Get("CannotCastForSeconds", magic.Name,
                                ((magic.CastTime + magic.Delay) - CMain.Time - 1) / 1000 + 1));
                        }

                        return;
                    }
                    magic.CastTime = CMain.Time;
                    break;
            }

            int cost;
            string prefix = actor == Hero ? LanguageMgr.Get("Hero"): string.Empty;
            // 214 条技能全部用原始 id：按效果 Spell 路由到已有的被动/开关分支。
            Spell effectSpell = SkillDescriptionHelper.EffectSpellOf(magic);
            if (effectSpell == Spell.无) effectSpell = magic.Spell;
            switch (effectSpell)
            {
                case Spell.基本剑术: // 基本剑术
                case Spell.致命剑: // 致命剑
                case Spell.吸魔: // 吸魔
                case Spell.出血: // 出血
                case Spell.精神力战法: // 精神力战法
                case Spell.攻杀剑术: // 攻杀剑术
                case Spell.专注: // 专注
                case Spell.冥想: // 冥想
                    return;
                case Spell.刺杀剑术: // 刺杀剑术
                    if (CMain.Time < ToggleTime) return;
                    bool wasThrusting = actor.Thrusting;
                    actor.Thrusting = fromHold ? true : !actor.Thrusting;
                    if (actor.Thrusting == wasThrusting) return;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.Thrusting ? LanguageMgr.Get("UseThrusting"): LanguageMgr.Get("DoNotUseThrusting")),
                        ChatType.System);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.Thrusting);
                    break;
                case Spell.半月弯刀: // 半月弯刀
                    if (CMain.Time < ToggleTime) return;
                    bool wasHalfMoon = actor.HalfMoon;
                    actor.HalfMoon = fromHold ? true : !actor.HalfMoon;
                    if (actor.HalfMoon == wasHalfMoon) return;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.HalfMoon ? LanguageMgr.Get("UseHalfMoon"): LanguageMgr.Get("DoNotUseHalfMoon")),
                        ChatType.System);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.HalfMoon);
                    break;
                case Spell.双半月: // 半月弯刀（双）
                    if (CMain.Time < ToggleTime) return;
                    bool wasCrossHalfMoon = actor.CrossHalfMoon;
                    actor.CrossHalfMoon = fromHold ? true : !actor.CrossHalfMoon;
                    if (actor.CrossHalfMoon == wasCrossHalfMoon) return;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.CrossHalfMoon ? LanguageMgr.Get("UseCrossHalfMoon"): LanguageMgr.Get("DoNotUseCrossHalfMoon")),
                        ChatType.System);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.CrossHalfMoon);
                    break;
                case Spell.双斩: // 双斩
                    if (CMain.Time < ToggleTime) return;
                    bool wasDoubleSlash = actor.DoubleSlash;
                    actor.DoubleSlash = fromHold ? true : !actor.DoubleSlash;
                    if (actor.DoubleSlash == wasDoubleSlash) return;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.DoubleSlash ? LanguageMgr.Get("UseDoubleSlash"): LanguageMgr.Get("DoNotUseDoubleSlash")),
                        ChatType.System);
                    ToggleTime = CMain.Time + 1000;
                    SendSpellToggle(actor, magic.Spell, actor.DoubleSlash);
                    break;
                case Spell.双烈火剑: // 双烈火剑
                    if (CMain.Time < ToggleTime) return;
                    ToggleTime = CMain.Time + 500;

                    cost = magic.Level * magic.LevelCost + magic.BaseCost;
                    if (cost > actor.MP)
                    {
                        Scene.OutputMessage(LanguageMgr.Get("LowMana"));
                        return;
                    }
                    actor.TwinDrakeBlade = true;
                    SendSpellToggle(actor, magic.Spell, true);
                    if (actor == Hero)
                        HeroObject?.Effects.Add(new Effect(Libraries.Magic2, 210, 6, 500, HeroObject));
                    else
                        actor.Effects.Add(new Effect(Libraries.Magic2, 210, 6, 500, actor));
                    break;
                case Spell.烈火剑法: // 烈火剑法
                    if (CMain.Time < ToggleTime) return;
                    ToggleTime = CMain.Time + 500;

                    cost = magic.Level * magic.LevelCost + magic.BaseCost;
                    if (cost > actor.MP)
                    {
                        Scene.OutputMessage(LanguageMgr.Get("LowMana"));
                        return;
                    }
                    SendSpellToggle(actor, magic.Spell, true);
                    break;
                case Spell.反击: // 反击
                    cost = magic.Level * magic.LevelCost + magic.BaseCost;
                    if (cost > actor.MP)
                    {
                        Scene.OutputMessage(LanguageMgr.Get("LowMana"));
                        return;
                    }

                    SoundManager.PlaySound(20000 + (ushort)Spell.反击 * 10);
                    SendSpellToggle(actor, magic.Spell, true);
                    break;
                case Spell.精神状态: // 精神状态
                    if (CMain.Time < ToggleTime) return;
                    ToggleTime = CMain.Time + 500;
                    SendSpellToggle(actor, magic.Spell, true);
                    break;
                default:
                    actor.NextMagic = magic;
                    actor.NextMagicLocation = MapControl.MapLocation;
                    actor.NextMagicDirection = MapControl.MouseDirection();

                    if (actor == Hero)
                        MapControl.UseMagic(Hero.NextMagic, Hero);
                    break;
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

        public override void ProcessPacket(Packet p)
        {
            switch (p.Index)
            {
                case (short)ServerPacketIds.KeepAlive:
                    KeepAlive((S.KeepAlive)p);
                    break;
                case (short)ServerPacketIds.MapInformation: //MapInfo
                    MapInformation((S.MapInformation)p);
                    break;
                case (short)ServerPacketIds.NewMapInfo:
                    NewMapInfo((S.NewMapInfo)p);
                    break;
                case (short)ServerPacketIds.WorldMapSetup:
                    WorldMapSetup((S.WorldMapSetupInfo)p);
                    break;
                case (short)ServerPacketIds.SearchMapResult:
                    SearchMapResult((S.SearchMapResult)p);
                    break;
                case (short)ServerPacketIds.UserInformation:
                    UserInformation((S.UserInformation)p);
                    break;
                case (short)ServerPacketIds.UserSlotsRefresh:
                    UserSlotsRefresh((S.UserSlotsRefresh)p);
                    break;
                case (short)ServerPacketIds.UserLocation:
                    UserLocation((S.UserLocation)p);
                    break;
                case (short)ServerPacketIds.ObjectPlayer:
                    ObjectPlayer((S.ObjectPlayer)p);
                    break;
                case (short)ServerPacketIds.ObjectHero:
                    ObjectHero((S.ObjectHero)p);
                    break;
                case (short)ServerPacketIds.ObjectRemove:
                    ObjectRemove((S.ObjectRemove)p);
                    break;
                case (short)ServerPacketIds.ObjectTurn:
                    ObjectTurn((S.ObjectTurn)p);
                    break;
                case (short)ServerPacketIds.ObjectWalk:
                    ObjectWalk((S.ObjectWalk)p);
                    break;
                case (short)ServerPacketIds.ObjectRun:
                    ObjectRun((S.ObjectRun)p);
                    break;
                case (short)ServerPacketIds.Chat:
                    ReceiveChat((S.Chat)p);
                    break;
                case (short)ServerPacketIds.ObjectChat:
                    ObjectChat((S.ObjectChat)p);
                    break;
                case (short)ServerPacketIds.MoveItem:
                    MoveItem((S.MoveItem)p);
                    break;
                case (short)ServerPacketIds.RefreshInventory:
                    ApplyRefreshedInventory((S.RefreshInventory)p);
                    break;
                case (short)ServerPacketIds.EquipItem:
                    EquipItem((S.EquipItem)p);
                    break;
                case (short)ServerPacketIds.MergeItem:
                    MergeItem((S.MergeItem)p);
                    break;
                case (short)ServerPacketIds.RemoveItem:
                    RemoveItem((S.RemoveItem)p);
                    break;
                case (short)ServerPacketIds.RemoveSlotItem:
                    RemoveSlotItem((S.RemoveSlotItem)p);
                    break;
                case (short)ServerPacketIds.TakeBackItem:
                    TakeBackItem((S.TakeBackItem)p);
                    break;
                case (short)ServerPacketIds.StoreItem:
                    StoreItem((S.StoreItem)p);
                    break;
                case (short)ServerPacketIds.DepositRefineItem:
                    DepositRefineItem((S.DepositRefineItem)p);
                    break;
                case (short)ServerPacketIds.RetrieveRefineItem:
                    RetrieveRefineItem((S.RetrieveRefineItem)p);
                    break;
                case (short)ServerPacketIds.RefineCancel:
                    RefineCancel((S.RefineCancel)p);
                    break;
                case (short)ServerPacketIds.RefineItem:
                    RefineItem((S.RefineItem)p);
                    break;
                case (short)ServerPacketIds.DepositTradeItem:
                    DepositTradeItem((S.DepositTradeItem)p);
                    break;
                case (short)ServerPacketIds.RetrieveTradeItem:
                    RetrieveTradeItem((S.RetrieveTradeItem)p);
                    break;
                case (short)ServerPacketIds.SplitItem:
                    SplitItem((S.SplitItem)p);
                    break;
                case (short)ServerPacketIds.SplitItem1:
                    SplitItem1((S.SplitItem1)p);
                    break;
                case (short)ServerPacketIds.UseItem:
                    UseItem((S.UseItem)p);
                    break;
                case (short)ServerPacketIds.DropItem:
                    DropItem((S.DropItem)p);
                    break;
                case (short)ServerPacketIds.TakeBackHeroItem:
                    TakeBackHeroItem((S.TakeBackHeroItem)p);
                    break;
                case (short)ServerPacketIds.TransferHeroItem:
                    TransferHeroItem((S.TransferHeroItem)p);
                    break;
                case (short)ServerPacketIds.PlayerUpdate:
                    PlayerUpdate((S.PlayerUpdate)p);
                    break;
                case (short)ServerPacketIds.PlayerInspect:
                    PlayerInspect((S.PlayerInspect)p);
                    break;
                case (short)ServerPacketIds.LogOutSuccess:
                    LogOutSuccess((S.LogOutSuccess)p);
                    break;
                case (short)ServerPacketIds.LogOutFailed:
                    LogOutFailed((S.LogOutFailed)p);
                    break;
                case (short)ServerPacketIds.ReturnToLogin:
                    ReturnToLogin((S.ReturnToLogin)p);
                    break;
                case (short)ServerPacketIds.ChangeAMode:
                    ChangeAMode((S.ChangeAMode)p);
                    break;
                case (short)ServerPacketIds.ChangePMode:
                    ChangePMode((S.ChangePMode)p);
                    break;
                case (short)ServerPacketIds.ObjectItem:
                    ObjectItem((S.ObjectItem)p);
                    break;
                case (short)ServerPacketIds.ObjectGold:
                    ObjectGold((S.ObjectGold)p);
                    break;
                case (short)ServerPacketIds.GainedItem:
                    GainedItem((S.GainedItem)p);
                    break;
                case (short)ServerPacketIds.GainedGold:
                    GainedGold((S.GainedGold)p);
                    break;
                case (short)ServerPacketIds.LoseGold:
                    LoseGold((S.LoseGold)p);
                    break;
                case (short)ServerPacketIds.GainedCredit:
                    GainedCredit((S.GainedCredit)p);
                    break;
                case (short)ServerPacketIds.LoseCredit:
                    LoseCredit((S.LoseCredit)p);
                    break;
                case (short)ServerPacketIds.GainedLegendCoin:
                    GainedLegendCoin((S.GainedLegendCoin)p);
                    break;
                case (short)ServerPacketIds.LoseLegendCoin:
                    LoseLegendCoin((S.LoseLegendCoin)p);
                    break;
                case (short)ServerPacketIds.ObjectMonster:
                    ObjectMonster((S.ObjectMonster)p);
                    break;
                case (short)ServerPacketIds.ObjectAttack:
                    ObjectAttack((S.ObjectAttack)p);
                    break;
                case (short)ServerPacketIds.Struck:
                    Struck((S.Struck)p);
                    break;
                case (short)ServerPacketIds.DamageIndicator:
                    DamageIndicator((S.DamageIndicator)p);
                    break;
                case (short)ServerPacketIds.ObjectStruck:
                    ObjectStruck((S.ObjectStruck)p);
                    break;
                case (short)ServerPacketIds.DuraChanged:
                    DuraChanged((S.DuraChanged)p);
                    break;
                case (short)ServerPacketIds.HealthChanged:
                    HealthChanged((S.HealthChanged)p);
                    break;
                case (short)ServerPacketIds.HeroHealthChanged:
                    HeroHealthChanged((S.HeroHealthChanged)p);
                    break;
                case (short)ServerPacketIds.DeleteItem:
                    DeleteItem((S.DeleteItem)p);
                    break;
                case (short)ServerPacketIds.Death:
                    Death((S.Death)p);
                    break;
                case (short)ServerPacketIds.ObjectDied:
                    ObjectDied((S.ObjectDied)p);
                    break;
                case (short)ServerPacketIds.ColourChanged:
                    ColourChanged((S.ColourChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectColourChanged:
                    ObjectColourChanged((S.ObjectColourChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectGuildNameChanged:
                    ObjectGuildNameChanged((S.ObjectGuildNameChanged)p);
                    break;
                case (short)ServerPacketIds.GainExperience:
                    GainExperience((S.GainExperience)p);
                    break;
                case (short)ServerPacketIds.GainHeroExperience:
                    GainHeroExperience((S.GainHeroExperience)p);
                    break;
                case (short)ServerPacketIds.LevelChanged:
                    LevelChanged((S.LevelChanged)p);
                    break;
                case (short)ServerPacketIds.HeroLevelChanged:
                    HeroLevelChanged((S.HeroLevelChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectLeveled:
                    ObjectLeveled((S.ObjectLeveled)p);
                    break;
                case (short)ServerPacketIds.ObjectHarvest:
                    ObjectHarvest((S.ObjectHarvest)p);
                    break;
                case (short)ServerPacketIds.ObjectHarvested:
                    ObjectHarvested((S.ObjectHarvested)p);
                    break;
                case (short)ServerPacketIds.ObjectNpc:
                    ObjectNPC((S.ObjectNPC)p);
                    break;
                case (short)ServerPacketIds.NPCResponse:
                    NPCResponse((S.NPCResponse)p);
                    break;
                case (short)ServerPacketIds.ObjectHide:
                    ObjectHide((S.ObjectHide)p);
                    break;
                case (short)ServerPacketIds.ObjectShow:
                    ObjectShow((S.ObjectShow)p);
                    break;
                case (short)ServerPacketIds.Poisoned:
                    Poisoned((S.Poisoned)p);
                    break;
                case (short)ServerPacketIds.ObjectPoisoned:
                    ObjectPoisoned((S.ObjectPoisoned)p);
                    break;
                case (short)ServerPacketIds.MapChanged:
                    MapChanged((S.MapChanged)p);
                    break;
                case (short)ServerPacketIds.ObjectTeleportOut:
                    ObjectTeleportOut((S.ObjectTeleportOut)p);
                    break;
                case (short)ServerPacketIds.ObjectTeleportIn:
                    ObjectTeleportIn((S.ObjectTeleportIn)p);
                    break;
                case (short)ServerPacketIds.TeleportIn:
                    TeleportIn();
                    break;
                case (short)ServerPacketIds.NPCGoods:
                    NPCGoods((S.NPCGoods)p);
                    break;
                case (short)ServerPacketIds.NPCSell:
                    NPCSell();
                    break;
                case (short)ServerPacketIds.NPCRepair:
                    NPCRepair((S.NPCRepair)p);
                    break;
                case (short)ServerPacketIds.NPCSRepair:
                    NPCSRepair((S.NPCSRepair)p);
                    break;
                case (short)ServerPacketIds.NPCRefine:
                    NPCRefine((S.NPCRefine)p);
                    break;
                case (short)ServerPacketIds.NPCCheckRefine:
                    NPCCheckRefine((S.NPCCheckRefine)p);
                    break;
                case (short)ServerPacketIds.NPCCollectRefine:
                    NPCCollectRefine((S.NPCCollectRefine)p);
                    break;
                case (short)ServerPacketIds.NPCReplaceWedRing:
                    NPCReplaceWedRing((S.NPCReplaceWedRing)p);
                    break;
                case (short)ServerPacketIds.NPCStorage:
                    NPCStorage();
                    break;
                case (short)ServerPacketIds.StorageUnlockResult:
                    StorageUnlockResult((S.StorageUnlockResult)p);
                    break;
                case (short)ServerPacketIds.StoragePasswordResult:
                    StoragePasswordResult((S.StoragePasswordResult)p);
                    break;
                case (short)ServerPacketIds.NPCRequestInput:
                    NPCRequestInput((S.NPCRequestInput)p);
                    break;
                case (short)ServerPacketIds.SellItem:
                    SellItem((S.SellItem)p);
                    break;
                case (short)ServerPacketIds.CraftItem:
                    CraftItem((S.CraftItem)p);
                    break;
                case (short)ServerPacketIds.RepairItem:
                    RepairItem((S.RepairItem)p);
                    break;
                case (short)ServerPacketIds.ItemRepaired:
                    ItemRepaired((S.ItemRepaired)p);
                    break;
                case (short)ServerPacketIds.ItemSlotSizeChanged:
                    ItemSlotSizeChanged((S.ItemSlotSizeChanged)p);
                    break;
                case (short)ServerPacketIds.ItemSealChanged:
                    ItemSealChanged((S.ItemSealChanged)p);
                    break;
                case (short)ServerPacketIds.NewMagic:
                    NewMagic((S.NewMagic)p);
                    break;
                case (short)ServerPacketIds.MagicLeveled:
                    MagicLeveled((S.MagicLeveled)p);
                    break;
                case (short)ServerPacketIds.Magic:
                    Magic((S.Magic)p);
                    break;
                case (short)ServerPacketIds.MagicDelay:
                    MagicDelay((S.MagicDelay)p);
                    break;
                case (short)ServerPacketIds.MagicCast:
                    MagicCast((S.MagicCast)p);
                    break;
                case (short)ServerPacketIds.ObjectMagic:
                    ObjectMagic((S.ObjectMagic)p);
                    break;
                case (short)ServerPacketIds.ObjectProjectile:
                    ObjectProjectile((S.ObjectProjectile)p);
                    break;
                case (short)ServerPacketIds.ObjectEffect:
                    ObjectEffect((S.ObjectEffect)p);
                    break;
                case (short)ServerPacketIds.RangeAttack:
                    RangeAttack((S.RangeAttack)p);
                    break;
                case (short)ServerPacketIds.Pushed:
                    Pushed((S.Pushed)p);
                    break;
                case (short)ServerPacketIds.ObjectPushed:
                    ObjectPushed((S.ObjectPushed)p);
                    break;
                case (short)ServerPacketIds.ObjectName:
                    ObjectName((S.ObjectName)p);
                    break;
                case (short)ServerPacketIds.UserStorage:
                    UserStorage((S.UserStorage)p);
                    break;
                case (short)ServerPacketIds.SwitchGroup:
                    SwitchGroup((S.SwitchGroup)p);
                    break;
                case (short)ServerPacketIds.DeleteGroup:
                    DeleteGroup();
                    break;
                case (short)ServerPacketIds.DeleteMember:
                    DeleteMember((S.DeleteMember)p);
                    break;
                case (short)ServerPacketIds.GroupInvite:
                    GroupInvite((S.GroupInvite)p);
                    break;
                case (short)ServerPacketIds.AddMember:
                    AddMember((S.AddMember)p);
                    break;
                case (short)ServerPacketIds.GroupMembersMap:
                    GroupMembersMap((S.GroupMembersMap)p);
                    break;
                case (short)ServerPacketIds.SendMemberLocation:
                    SendMemberLocation((S.SendMemberLocation)p);
                    break;
                case (short)ServerPacketIds.Revived:
                    Revived();
                    break;
                case (short)ServerPacketIds.ObjectRevived:
                    ObjectRevived((S.ObjectRevived)p);
                    break;
                case (short)ServerPacketIds.SpellToggle:
                    SpellToggle((S.SpellToggle)p);
                    break;
                case (short)ServerPacketIds.ObjectHealth:
                    ObjectHealth((S.ObjectHealth)p);
                    break;
                case (short)ServerPacketIds.ObjectMana:
                    ObjectMana((S.ObjectMana)p);
                    break;
                case (short)ServerPacketIds.MapEffect:
                    MapEffect((S.MapEffect)p);
                    break;
                case (short)ServerPacketIds.AllowObserve:
                    AllowObserve = ((S.AllowObserve)p).Allow;
                    break;
                case (short)ServerPacketIds.ObjectRangeAttack:
                    ObjectRangeAttack((S.ObjectRangeAttack)p);
                    break;
                case (short)ServerPacketIds.AddBuff:
                    AddBuff((S.AddBuff)p);
                    break;
                case (short)ServerPacketIds.RemoveBuff:
                    RemoveBuff((S.RemoveBuff)p);
                    break;
                case (short)ServerPacketIds.PauseBuff:
                    PauseBuff((S.PauseBuff)p);
                    break;
                case (short)ServerPacketIds.ObjectHidden:
                    ObjectHidden((S.ObjectHidden)p);
                    break;
                case (short)ServerPacketIds.RefreshItem:
                    RefreshItem((S.RefreshItem)p);
                    break;
                case (short)ServerPacketIds.ObjectSpell:
                    ObjectSpell((S.ObjectSpell)p);
                    break;
                case (short)ServerPacketIds.UserDash:
                    UserDash((S.UserDash)p);
                    break;
                case (short)ServerPacketIds.ObjectDash:
                    ObjectDash((S.ObjectDash)p);
                    break;
                case (short)ServerPacketIds.UserDashFail:
                    UserDashFail((S.UserDashFail)p);
                    break;
                case (short)ServerPacketIds.ObjectDashFail:
                    ObjectDashFail((S.ObjectDashFail)p);
                    break;
                case (short)ServerPacketIds.NPCConsign:
                    NPCConsign();
                    break;
                case (short)ServerPacketIds.NPCMarket:
                    NPCMarket((S.NPCMarket)p);
                    break;
                case (short)ServerPacketIds.NPCMarketPage:
                    NPCMarketPage((S.NPCMarketPage)p);
                    break;
                case (short)ServerPacketIds.ConsignItem:
                    ConsignItem((S.ConsignItem)p);
                    break;
                case (short)ServerPacketIds.MarketFail:
                    MarketFail((S.MarketFail)p);
                    break;
                case (short)ServerPacketIds.MarketSuccess:
                    MarketSuccess((S.MarketSuccess)p);
                    break;
                case (short)ServerPacketIds.ObjectSitDown:
                    ObjectSitDown((S.ObjectSitDown)p);
                    break;
                case (short)ServerPacketIds.InTrapRock:
                    S.InTrapRock packetdata = (S.InTrapRock)p;
                    User.InTrapRock = packetdata.Trapped;
                    break;
                case (short)ServerPacketIds.RemoveMagic:
                    RemoveMagic((S.RemoveMagic)p);
                    break;
                case (short)ServerPacketIds.BaseStatsInfo:
                    BaseStatsInfo((S.BaseStatsInfo)p);
                    break;
                case (short)ServerPacketIds.HeroBaseStatsInfo:
                    HeroBaseStatsInfo((S.HeroBaseStatsInfo)p);
                    break;
                case (short)ServerPacketIds.UserName:
                    UserName((S.UserName)p);
                    break;
                case (short)ServerPacketIds.ChatItemStats:
                    ChatItemStats((S.ChatItemStats)p);
                    break;
                case (short)ServerPacketIds.GuildInvite:
                    GuildInvite((S.GuildInvite)p);
                    break;
                case (short)ServerPacketIds.GuildMemberChange:
                    GuildMemberChange((S.GuildMemberChange)p);
                    break;
                case (short)ServerPacketIds.GuildNoticeChange:
                    GuildNoticeChange((S.GuildNoticeChange)p);
                    break;
                case (short)ServerPacketIds.GuildStatus:
                    GuildStatus((S.GuildStatus)p);
                    break;
                case (short)ServerPacketIds.GuildExpGain:
                    GuildExpGain((S.GuildExpGain)p);
                    break;
                case (short)ServerPacketIds.GuildNameRequest:
                    GuildNameRequest((S.GuildNameRequest)p);
                    break;
                case (short)ServerPacketIds.GuildStorageGoldChange:
                    GuildStorageGoldChange((S.GuildStorageGoldChange)p);
                    break;
                case (short)ServerPacketIds.GuildStorageItemChange:
                    GuildStorageItemChange((S.GuildStorageItemChange)p);
                    break;
                case (short)ServerPacketIds.GuildStorageList:
                    GuildStorageList((S.GuildStorageList)p);
                    break;
                case (short)ServerPacketIds.GuildRequestWar:
                    GuildRequestWar((S.GuildRequestWar)p);
                    break;
                case (short)ServerPacketIds.HeroCreateRequest:
                    HeroCreateRequest((S.HeroCreateRequest)p);
                    break;
                case (short)ServerPacketIds.NewHero:
                    NewHero((S.NewHero)p);
                    break;
                case (short)ServerPacketIds.HeroInformation:
                    HeroInformation((S.HeroInformation)p);
                    break;
                case (short)ServerPacketIds.UpdateHeroSpawnState:
                    UpdateHeroSpawnState((S.UpdateHeroSpawnState)p);
                    break;
                case (short)ServerPacketIds.UnlockHeroAutoPot:
                    UnlockHeroAutoPot(true);
                    break;
                case (short)ServerPacketIds.SetAutoPotValue:
                    SetAutoPotValue((S.SetAutoPotValue)p);
                    break;
                case (short)ServerPacketIds.SetHeroBehaviour:
                    SetHeroBehaviour((S.SetHeroBehaviour)p);
                    break;
                case (short)ServerPacketIds.SetAutoPotItem:
                    SetAutoPotItem((S.SetAutoPotItem)p);
                    break;
                case (short)ServerPacketIds.ManageHeroes:
                    ManageHeroes((S.ManageHeroes)p);
                    break;
                case (short)ServerPacketIds.ChangeHero:
                    ChangeHero((S.ChangeHero)p);
                    break;
                case (short)ServerPacketIds.DefaultNPC:
                    DefaultNPC((S.DefaultNPC)p);
                    break;
                case (short)ServerPacketIds.NPCUpdate:
                    NPCUpdate((S.NPCUpdate)p);
                    break;
                case (short)ServerPacketIds.NPCImageUpdate:
                    NPCImageUpdate((S.NPCImageUpdate)p);
                    break;
                case (short)ServerPacketIds.MarriageRequest:
                    MarriageRequest((S.MarriageRequest)p);
                    break;
                case (short)ServerPacketIds.DivorceRequest:
                    DivorceRequest((S.DivorceRequest)p);
                    break;
                case (short)ServerPacketIds.MentorRequest:
                    MentorRequest((S.MentorRequest)p);
                    break;
                case (short)ServerPacketIds.TradeRequest:
                    TradeRequest((S.TradeRequest)p);
                    break;
                case (short)ServerPacketIds.TradeAccept:
                    TradeAccept((S.TradeAccept)p);
                    break;
                case (short)ServerPacketIds.TradeGold:
                    TradeGold((S.TradeGold)p);
                    break;
                case (short)ServerPacketIds.TradeItem:
                    TradeItem((S.TradeItem)p);
                    break;
                case (short)ServerPacketIds.TradeConfirm:
                    TradeConfirm();
                    break;
                case (short)ServerPacketIds.TradeCancel:
                    TradeCancel((S.TradeCancel)p);
                    break;
                case (short)ServerPacketIds.MountUpdate:
                    MountUpdate((S.MountUpdate)p);
                    break;
                case (short)ServerPacketIds.TransformUpdate:
                    TransformUpdate((S.TransformUpdate)p);
                    break;
                case (short)ServerPacketIds.EquipSlotItem:
                    EquipSlotItem((S.EquipSlotItem)p);
                    break;
                case (short)ServerPacketIds.FishingUpdate:
                    FishingUpdate((S.FishingUpdate)p);
                    break;
                case (short)ServerPacketIds.ChangeQuest:
                    ChangeQuest((S.ChangeQuest)p);
                    break;
                case (short)ServerPacketIds.GainedQuestItem:
                    GainedQuestItem((S.GainedQuestItem)p);
                    break;
                case (short)ServerPacketIds.DeleteQuestItem:
                    DeleteQuestItem((S.DeleteQuestItem)p);
                    break;
                case (short)ServerPacketIds.CancelReincarnation:
                    User.ReincarnationStopTime = 0;
                    break;
                case (short)ServerPacketIds.RequestReincarnation:
                    if (!User.Dead) return;
                    RequestReincarnation();
                    break;
                case (short)ServerPacketIds.UserBackStep:
                    UserBackStep((S.UserBackStep)p);
                    break;
                case (short)ServerPacketIds.ObjectBackStep:
                    ObjectBackStep((S.ObjectBackStep)p);
                    break;
                case (short)ServerPacketIds.UserDashAttack:
                    UserDashAttack((S.UserDashAttack)p);
                    break;
                case (short)ServerPacketIds.ObjectDashAttack:
                    ObjectDashAttack((S.ObjectDashAttack)p);
                    break;
                case (short)ServerPacketIds.UserAttackMove://Warrior Skill - SlashingBurst
                    UserAttackMove((S.UserAttackMove)p);
                    break;
                case (short)ServerPacketIds.CombineItem:
                    CombineItem((S.CombineItem)p);
                    break;
                case (short)ServerPacketIds.ItemUpgraded:
                    ItemUpgraded((S.ItemUpgraded)p);
                    break;
                case (short)ServerPacketIds.SetConcentration:
                    SetConcentration((S.SetConcentration)p);
                    break;
                case (short)ServerPacketIds.SetElemental:
                    SetElemental((S.SetElemental)p);
                    break;
                case (short)ServerPacketIds.RemoveDelayedExplosion:
                    RemoveDelayedExplosion((S.RemoveDelayedExplosion)p);
                    break;
                case (short)ServerPacketIds.ObjectDeco:
                    ObjectDeco((S.ObjectDeco)p);
                    break;
                case (short)ServerPacketIds.ObjectSneaking:
                    ObjectSneaking((S.ObjectSneaking)p);
                    break;
                case (short)ServerPacketIds.ObjectLevelEffects:
                    ObjectLevelEffects((S.ObjectLevelEffects)p);
                    break;
                case (short)ServerPacketIds.SetBindingShot:
                    SetBindingShot((S.SetBindingShot)p);
                    break;
                case (short)ServerPacketIds.SendOutputMessage:
                    SendOutputMessage((S.SendOutputMessage)p);
                    break;
                case (short)ServerPacketIds.NPCAwakening:
                    NPCAwakening();
                    break;
                case (short)ServerPacketIds.NPCDisassemble:
                    NPCDisassemble();
                    break;
                case (short)ServerPacketIds.NPCDowngrade:
                    NPCDowngrade();
                    break;
                case (short)ServerPacketIds.NPCReset:
                    NPCReset();
                    break;
                case (short)ServerPacketIds.AwakeningNeedMaterials:
                    AwakeningNeedMaterials((S.AwakeningNeedMaterials)p);
                    break;
                case (short)ServerPacketIds.AwakeningLockedItem:
                    AwakeningLockedItem((S.AwakeningLockedItem)p);
                    break;
                case (short)ServerPacketIds.Awakening:
                    Awakening((S.Awakening)p);
                    break;
                case (short)ServerPacketIds.ReceiveMail:
                    ReceiveMail((S.ReceiveMail)p);
                    break;
                case (short)ServerPacketIds.MailLockedItem:
                    MailLockedItem((S.MailLockedItem)p);
                    break;
                case (short)ServerPacketIds.MailSent:
                    MailSent((S.MailSent)p);
                    break;
                case (short)ServerPacketIds.MailSendRequest:
                    MailSendRequest((S.MailSendRequest)p);
                    break;
                case (short)ServerPacketIds.ParcelCollected:
                    ParcelCollected((S.ParcelCollected)p);
                    break;
                case (short)ServerPacketIds.MailCost:
                    MailCost((S.MailCost)p);
                    break;
                case (short)ServerPacketIds.ResizeInventory:
                    ResizeInventory((S.ResizeInventory)p);
                    break;
                case (short)ServerPacketIds.ResizeStorage:
                    ResizeStorage((S.ResizeStorage)p);
                    break;
                case (short)ServerPacketIds.NewIntelligentCreature:
                    NewIntelligentCreature((S.NewIntelligentCreature)p);
                    break;
                case (short)ServerPacketIds.UpdateIntelligentCreatureList:
                    UpdateIntelligentCreatureList((S.UpdateIntelligentCreatureList)p);
                    break;
                case (short)ServerPacketIds.IntelligentCreatureEnableRename:
                    IntelligentCreatureEnableRename((S.IntelligentCreatureEnableRename)p);
                    break;
                case (short)ServerPacketIds.IntelligentCreaturePickup:
                    IntelligentCreaturePickup((S.IntelligentCreaturePickup)p);
                    break;
                case (short)ServerPacketIds.NPCPearlGoods:
                    NPCPearlGoods((S.NPCPearlGoods)p);
                    break;
                case (short)ServerPacketIds.FriendUpdate:
                    FriendUpdate((S.FriendUpdate)p);
                    break;
                case (short)ServerPacketIds.LoverUpdate:
                    LoverUpdate((S.LoverUpdate)p);
                    break;
                case (short)ServerPacketIds.MentorUpdate:
                    MentorUpdate((S.MentorUpdate)p);
                    break;
                case (short)ServerPacketIds.GuildBuffList:
                    GuildBuffList((S.GuildBuffList)p);
                    break;
                case (short)ServerPacketIds.GameShopInfo:
                    GameShopUpdate((S.GameShopInfo)p);
                    break;
                case (short)ServerPacketIds.GameShopStock:
                    GameShopStock((S.GameShopStock)p);
                    break;
                case (short)ServerPacketIds.Rankings:
                    Rankings((S.Rankings)p);
                    break;
                case (short)ServerPacketIds.Opendoor:
                    Opendoor((S.Opendoor)p);
                    break;
                case (short)ServerPacketIds.GetRentedItems:
                    RentedItems((S.GetRentedItems)p);
                    break;
                case (short)ServerPacketIds.ItemRentalRequest:
                    ItemRentalRequest((S.ItemRentalRequest)p);
                    break;
                case (short)ServerPacketIds.ItemRentalFee:
                    ItemRentalFee((S.ItemRentalFee)p);
                    break;
                case (short)ServerPacketIds.ItemRentalPeriod:
                    ItemRentalPeriod((S.ItemRentalPeriod)p);
                    break;
                case (short)ServerPacketIds.DepositRentalItem:
                    DepositRentalItem((S.DepositRentalItem)p);
                    break;
                case (short)ServerPacketIds.RetrieveRentalItem:
                    RetrieveRentalItem((S.RetrieveRentalItem)p);
                    break;
                case (short)ServerPacketIds.UpdateRentalItem:
                    UpdateRentalItem((S.UpdateRentalItem)p);
                    break;
                case (short)ServerPacketIds.CancelItemRental:
                    CancelItemRental((S.CancelItemRental)p);
                    break;
                case (short)ServerPacketIds.ItemRentalLock:
                    ItemRentalLock((S.ItemRentalLock)p);
                    break;
                case (short)ServerPacketIds.ItemRentalPartnerLock:
                    ItemRentalPartnerLock((S.ItemRentalPartnerLock)p);
                    break;
                case (short)ServerPacketIds.CanConfirmItemRental:
                    CanConfirmItemRental((S.CanConfirmItemRental)p);
                    break;
                case (short)ServerPacketIds.ConfirmItemRental:
                    ConfirmItemRental((S.ConfirmItemRental)p);
                    break;
                case (short)ServerPacketIds.OpenBrowser:
                    OpenBrowser((S.OpenBrowser)p);
                    break;
                case (short)ServerPacketIds.PlaySound:
                    PlaySound((S.PlaySound)p);
                    break;
                case (short)ServerPacketIds.SetTimer:
                    SetTimer((S.SetTimer)p);
                    break;
                case (short)ServerPacketIds.ExpireTimer:
                    ExpireTimer((S.ExpireTimer)p);
                    break;
                case (short)ServerPacketIds.UpdateNotice:
                    ShowWelcomeNotice((S.UpdateNotice)p);
                    break;
                case (short)ServerPacketIds.Roll:
                    Roll((S.Roll)p);
                    break;
                case (short)ServerPacketIds.SetCompass:
                    SetCompass((S.SetCompass)p);
                    break;
                case (short)ServerPacketIds.GuildTerritoryPage:
                    GuildTerritoryPage((S.GuildTerritoryPage)p);
                    break;
                case (short)ServerPacketIds.NewMonsterInfo:
                    NewMonsterInfo((S.NewMonsterInfo)p);
                    break;
                case (short)ServerPacketIds.NewNPCInfo:
                    NewNPCInfo((S.NewNPCInfo)p);
                    break;
                default:
                    base.ProcessPacket(p);
                    break;
            }
        }

    }
}
