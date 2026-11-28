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
        private void GainedQuestItem(S.GainedQuestItem p)
        {
            Bind(p.Item);
            AddQuestItem(p.Item);
        }

        private void GainedGold(S.GainedGold p)
        {
            if (p.Gold == 0) return;

            Gold += p.Gold;
            SoundManager.PlaySound(SoundList.Gold);
            OutputMessage($"<color=lime>获得</color> 金币 x{p.Gold}");
        }
        private void LoseGold(S.LoseGold p)
        {
            Gold -= p.Gold;
            SoundManager.PlaySound(SoundList.Gold);
        }
        private void GainedCredit(S.GainedCredit p)
        {
            if (p.Credit == 0) return;

            Credit += p.Credit;
            SoundManager.PlaySound(SoundList.Gold);
            OutputMessage($"<color=lime>获得</color> 元宝 x{p.Credit}");
        }
        private void LoseCredit(S.LoseCredit p)
        {
            Credit -= p.Credit;
            SoundManager.PlaySound(SoundList.Gold);
        }
        private void GainedLegendCoin(S.GainedLegendCoin p)
        {
            if (p.LegendCoin == 0) return;

            LegendCoin += p.LegendCoin;
            SoundManager.PlaySound(SoundList.Gold);
            OutputMessage($"<color=lime>获得</color> 传奇币 x{p.LegendCoin}");
        }
        private void LoseLegendCoin(S.LoseLegendCoin p)
        {
            LegendCoin -= p.LegendCoin;
            SoundManager.PlaySound(SoundList.Gold);
        }
        private void ObjectMonster(S.ObjectMonster p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob) && ob is MonsterObject mob)
            {
                PrintTool.Log($"DBG:ObjectMonster id={p.ObjectID} p.Dead={p.Dead} p.Skel={p.Skeleton} mob.Dead={mob.Dead}");
                // 已死亡的尸体不被“还活着”的刷新包复活：否则会被重新 Load，Dead 被覆盖回 false，
                // SetAction() 把怪物重置成 Standing→尸体站起来播待机。仅当刷新包也确认死亡/化为骸骨时才允许更新
                //（尸体→骸骨是合法转换）。
                if (mob.Dead && !p.Dead && !p.Skeleton)
                    return;
                mob.Load(p, true);
                return;
            }

            mob = new MonsterObject(p.ObjectID);
            PrintTool.Log($"DBG:ObjectMonster-SPAWN id={p.ObjectID} p.Dead={p.Dead} p.Skel={p.Skeleton} originModel={mob.OriginModel} name={p.Name}");
            mob.Load(p, false);
        }

        private void ObjectAttack(S.ObjectAttack p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var atkOb) && atkOb.Race == ObjectType.Monster)
                PrintTool.Log($"DBG:ObjectAttack id={p.ObjectID} type={p.Type}");

            QueuedAction action = null;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                if (ob.Race == ObjectType.Player)
                {
                    action = new QueuedAction { Action = MirAction.Attack1, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                }
                else
                {
                    switch (p.Type)
                    {
                        default:
                            {
                                action = new QueuedAction { Action = MirAction.Attack1, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 1:
                            {
                                action = new QueuedAction { Action = MirAction.Attack2, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 2:
                            {
                                action = new QueuedAction { Action = MirAction.Attack3, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 3:
                            {
                                action = new QueuedAction { Action = MirAction.Attack4, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                        case 4:
                            {
                                action = new QueuedAction { Action = MirAction.Attack5, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                                break;
                            }
                    }
                }
                action.Params.Add(p.Spell);
                action.Params.Add(p.Level);
                ob.ActionFeed.Add(action);
            }
        }
        private void Struck(S.Struck p)
        {
            LogTime = CMain.Time + Globals.LogDelay;

            NextRunTime = CMain.Time + 2500;
            User.BlizzardStopTime = 0;
            User.ClearMagic();
            if (User.ReincarnationStopTime > CMain.Time)
                Network.Enqueue(new C.CancelReincarnation { });

            MirDirection dir = User.Direction;
            Point location = User.CurrentLocation;

            for (int i = 0; i < User.ActionFeed.Count; i++)
                if (User.ActionFeed[i].Action == MirAction.Struck) return;


            if (User.ActionFeed.Count > 0)
            {
                dir = User.ActionFeed[User.ActionFeed.Count - 1].Direction;
                location = User.ActionFeed[User.ActionFeed.Count - 1].Location;
            }

            if (User.Buffs.Any(a => a == BuffType.EnergyShield))
            {
                for (int j = 0; j < User.Effects.Count; j++)
                {
                    BuffEffect effect = null;
                    effect = User.Effects[j] as BuffEffect;

                    if (effect != null && effect.BuffType == BuffType.EnergyShield)
                    {
                        effect.Clear();
                        effect.Remove();

                        User.Effects.Add(effect = new BuffEffect(Libraries.Magic2, 1890, 6, 600, User, true, BuffType.EnergyShield) { Repeat = false });
                        SoundManager.PlaySound(20000 + (ushort)Spell.能量护盾 * 10 + 1);

                        effect.Complete += (o, e) =>
                        {
                            User.Effects.Add(new BuffEffect(Libraries.Magic2, 1900, 2, 800, User, true, BuffType.EnergyShield) { Repeat = true });
                        };


                        break;
                    }
                }
            }

            QueuedAction action = new QueuedAction
            {
                Action = MirAction.Struck,
                Direction = dir,
                Location = location,
                Params = new List<object>()
            };
            action.Params.Add(p.AttackerID);

            int weapon = -2;
            if (MapControl.Objects.TryGetValue(p.AttackerID, out MapObject attacker) &&
                attacker.Race == ObjectType.Player)
            {
                PlayerObject player = (PlayerObject)attacker;
                weapon = player.Weapon;
                if (player.Class == MirClass.Assassin && weapon > -1)
                    weapon = 1;
            }
            action.Params.Add(weapon);
            User.ActionFeed.Add(action);

        }
        private void ObjectStruck(S.ObjectStruck p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                // 原始 JS 用攻击者与目标的 X 坐标差决定 startarr/stoparr/endarr 的第几组。
                if (MapControl.Objects.TryGetValue(p.AttackerID, out MapObject damageAttacker))
                {
                    if (damageAttacker.CurrentLocation.X > p.Location.X)
                    {
                        ob.LastDamageSide = 1;
                    }
                    else if (damageAttacker.CurrentLocation.X < p.Location.X)
                    {
                        ob.LastDamageSide = 2;
                    }
                    else
                    {
                        ob.LastDamageSide = 0;
                    }
                }
                else
                {
                    ob.LastDamageSide = 0;
                }

                if (ob.Race == ObjectType.Monster)
                {
                    PrintTool.Log($"DBG:ObjectStruck id={p.ObjectID}");
                }

                if (ob.SkipFrames)
                {
                    return;
                }
                if (ob.ActionFeed.Count > 0 && ob.ActionFeed[ob.ActionFeed.Count - 1].Action == MirAction.Struck) return;

                if (ob.Race == ObjectType.Player)
                    ((PlayerObject)ob).BlizzardStopTime = 0;
                QueuedAction action = new QueuedAction
                {
                    Action = MirAction.Struck,
                    Direction = p.Direction,
                    Location = p.Location,
                    Params = new List<object>()
                };
                action.Params.Add(p.AttackerID);

                int weapon = -2;
                if (MapControl.Objects.TryGetValue(p.AttackerID, out MapObject attacker) &&
                    attacker.Race == ObjectType.Player)
                {
                    PlayerObject player = (PlayerObject)attacker;
                    weapon = player.Weapon;
                    if (player.Class == MirClass.Assassin && weapon > -1)
                        weapon = 1;
                }
                action.Params.Add(weapon);
                ob.ActionFeed.Add(action);

                if (ob.Buffs.Any(a => a == BuffType.EnergyShield))
                {
                    for (int j = 0; j < ob.Effects.Count; j++)
                    {
                        BuffEffect effect = null;
                        effect = ob.Effects[j] as BuffEffect;

                        if (effect != null && effect.BuffType == BuffType.EnergyShield)
                        {
                            effect.Clear();
                            effect.Remove();

                            ob.Effects.Add(effect = new BuffEffect(Libraries.Magic2, 1890, 6, 600, ob, true, BuffType.EnergyShield) { Repeat = false });
                            SoundManager.PlaySound(20000 + (ushort)Spell.能量护盾 * 10 + 1);

                            effect.Complete += (o, e) =>
                            {
                                ob.Effects.Add(new BuffEffect(Libraries.Magic2, 1900, 2, 800, ob, true, BuffType.EnergyShield) { Repeat = true });
                            };

                            break;
                        }
                    }
                }
            }
        }

        private void DamageIndicator(S.DamageIndicator p)
        {
            if (Settings.DisplayDamage)
            {
                if (MapControl.Objects.TryGetValue(p.ObjectID, out var obj))
                {
                    if (obj.Damages.Count >= 10)
                    {
                        return;
                    }

                    int side = obj.LastDamageSide;
                    int Piaozi_DamageId = p.Piaozi_DamageId;
                    switch (p.Type)
                    {
                        case DamageType.Hit: // 服务端下发技能 Piaozi_DamageId；0 表示原始传奇默认 damage id=0
                            obj.Damages.Add(new OriginMir2Damage(p.Damage.ToString(), 1000,
                                obj.Race == ObjectType.Player ? CommonColor.Red : CommonColor.White,
                                Piaozi_DamageId > 0 ? Piaozi_DamageId : OriginMir2Damage.DefaultHitPiaozi_DamageId, side));
                            break;
                        case DamageType.Miss: // 原始传奇 damage id=7：只画 plus_36“闪避”图标，不画小字
                            obj.Damages.Add(new OriginMir2Damage(string.Empty, 1200,
                                obj.Race == ObjectType.Player ? CommonColor.LightCoral : CommonColor.LightGray,
                                OriginMir2Damage.DefaultMissPiaozi_DamageId, side));
                            break;
                        case DamageType.Critical: // 服务端固定下发 damage id=1（number=1 + title=31“致命”图标）
                            obj.Damages.Add(new OriginMir2Damage(p.Damage.ToString(), 1000,
                                CommonColor.DarkRed, Piaozi_DamageId > 0 ? Piaozi_DamageId : OriginMir2Damage.DefaultCritPiaozi_DamageId, side) { Offset = 15 });
                            break;
                    }
                }
            }
        }

        private void DuraChanged(S.DuraChanged p)
        {
            UserItem item = null;
            for (int i = 0; i < User.Inventory.Length; i++)
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.UniqueID)
                {
                    item = User.Inventory[i];
                    break;
                }


            if (item == null)
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.UniqueID)
                    {
                        item = User.Equipment[i];
                        break;
                    }
                    if (User.Equipment[i] != null && User.Equipment[i].Slots != null)
                    {
                        for (int j = 0; j < User.Equipment[i].Slots.Length; j++)
                        {
                            if (User.Equipment[i].Slots[j] != null && User.Equipment[i].Slots[j].UniqueID == p.UniqueID)
                            {
                                item = User.Equipment[i].Slots[j];
                                break;
                            }
                        }

                        if (item != null) break;
                    }
                }

            if (item == null) return;

            item.CurrentDura = p.CurrentDura;

            if (item.CurrentDura == 0)
            {
                User.RefreshStats();
                switch (item.Info.Type)
                {
                    case ItemType.Mount:
                        ChatDialog.ReceiveChat(LanguageMgr.Get("FriendlyNoLongerLoyal", item.Info.FriendlyName), ChatType.System);
                        break;
                    default:
                        ChatDialog.ReceiveChat(LanguageMgr.Get("FriendlyDuraDroppedToZero", item.Info.FriendlyName), ChatType.System);
                        break;
                }

            }

            if (HoverItem == item)
            {
                DisposeItemLabel();
                CreateItemLabel(item);
            }
        }
        private void HealthChanged(S.HealthChanged p)
        {
            User.HP = p.HP;
            User.MP = p.MP;

            User.PercentHealth = (byte)(User.HP / (float)User.Stats[Stat.HP] * 100);
        }
        private void HeroHealthChanged(S.HeroHealthChanged p)
        {
            Hero.HP = p.HP;
            Hero.MP = p.MP;

            Hero.PercentHealth = (byte)(Hero.HP / (float)Hero.Stats[Stat.HP] * 100);
            Hero.PercentMana = (byte)(Hero.MP / (float)Hero.Stats[Stat.MP] * 100);
        }

        private void DeleteQuestItem(S.DeleteQuestItem p)
        {
            for (int i = 0; i < User.QuestInventory.Length; i++)
            {
                UserItem item = User.QuestInventory[i];

                if (item == null || item.UniqueID != p.UniqueID) continue;

                if (item.Count == p.Count)
                    User.QuestInventory[i] = null;
                else
                    item.Count -= p.Count;
                break;
            }
        }

        private void DeleteItem(S.DeleteItem p)
        {
            UserObject actor = null;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (actor != null) break;
                UserItem item = User.Inventory[i];

                if (item != null && item.Slots.Length > 0)
                {
                    for (int j = 0; j < item.Slots.Length; j++)
                    {
                        UserItem slotItem = item.Slots[j];

                        if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                        if (slotItem.Count == p.Count)
                            item.Slots[j] = null;
                        else
                            slotItem.Count -= p.Count;
                        actor = User;
                        break;
                    }
                }

                if (item == null || item.UniqueID != p.UniqueID) continue;

                if (item.Count == p.Count)
                    User.Inventory[i] = null;
                else
                    item.Count -= p.Count;
                actor = User;
            }

            if (actor == null)
            {
                for (int i = 0; i < User.Equipment.Length; i++)
                {
                    if (actor != null) break;
                    UserItem item = User.Equipment[i];

                    if (item != null && item.Slots.Length > 0)
                    {
                        for (int j = 0; j < item.Slots.Length; j++)
                        {
                            UserItem slotItem = item.Slots[j];

                            if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                            if (slotItem.Count == p.Count)
                                item.Slots[j] = null;
                            else
                                slotItem.Count -= p.Count;
                            actor = User;
                            break;
                        }
                    }

                    if (item == null || item.UniqueID != p.UniqueID) continue;

                    if (item.Count == p.Count)
                        User.Equipment[i] = null;
                    else
                        item.Count -= p.Count;
                    actor = User;
                }
            }

            if (Hero != null && actor == null)
            {
                for (int i = 0; i < Hero.Inventory.Length; i++)
                {
                    if (actor != null) break;
                    UserItem item = Hero.Inventory[i];

                    if (item != null && item.Slots.Length > 0)
                    {
                        for (int j = 0; j < item.Slots.Length; j++)
                        {
                            UserItem slotItem = item.Slots[j];

                            if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                            if (slotItem.Count == p.Count)
                                item.Slots[j] = null;
                            else
                                slotItem.Count -= p.Count;
                            actor = Hero;
                            break;
                        }
                    }

                    if (item == null || item.UniqueID != p.UniqueID) continue;

                    if (item.Count == p.Count)
                        User.Inventory[i] = null;
                    else
                        item.Count -= p.Count;
                    actor = Hero;
                }

                if (actor == null)
                {
                    for (int i = 0; i < Hero.Equipment.Length; i++)
                    {
                        if (actor != null) break;
                        UserItem item = Hero.Equipment[i];

                        if (item != null && item.Slots.Length > 0)
                        {
                            for (int j = 0; j < item.Slots.Length; j++)
                            {
                                UserItem slotItem = item.Slots[j];

                                if (slotItem == null || slotItem.UniqueID != p.UniqueID) continue;

                                if (slotItem.Count == p.Count)
                                    item.Slots[j] = null;
                                else
                                    slotItem.Count -= p.Count;
                                actor = Hero;
                                break;
                            }
                        }

                        if (item == null || item.UniqueID != p.UniqueID) continue;

                        if (item.Count == p.Count)
                            User.Equipment[i] = null;
                        else
                            item.Count -= p.Count;
                        actor = Hero;
                    }
                }
            }

            if (actor == null)
            {
                for (int i = 0; i < Storage.Length; i++)
                {
                    var item = Storage[i];
                    if (item == null || item.UniqueID != p.UniqueID) continue;

                    if (item.Count == p.Count)
                        Storage[i] = null;
                    else
                        item.Count -= p.Count;
                    break;
                }
            }
            actor?.RefreshStats();
        }
        private void Death(S.Death p)
        {
            User.Dead = true;

            User.ActionFeed.Add(new QueuedAction { Action = MirAction.Die, Direction = p.Direction, Location = p.Location });
            ShowReviveMessage = true;

            LogTime = 0;
        }
        private void ObjectDied(S.ObjectDied p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                switch (p.Type)
                {
                    default:
                        if (ob.Race == ObjectType.Monster) PrintTool.Log($"DBG:ObjectDied id={p.ObjectID} originModel={(ob is MonsterObject mo ? mo.OriginModel : 0)} name={ob.Name}");
                        ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Die, Direction = p.Direction, Location = p.Location });
                        ob.Dead = true;
                        break;
                    case 1:
                        MapControl.Effects.Add(new Effect(Libraries.Magic2, 690, 10, 1000, ob.CurrentLocation));
                        ob.Remove();
                        break;
                    case 2:
                        SoundManager.PlaySound(20000 + (ushort)Spell.暗黑化身 * 10 + 1);
                        MapControl.Effects.Add(new Effect(Libraries.Magic2, 2600, 10, 1200, ob.CurrentLocation));
                        ob.Remove();
                        break;
                }
            }
        }
        private void ColourChanged(S.ColourChanged p)
        {
            User.NameColour = p.NameColour;
        }
        private void ObjectColourChanged(S.ObjectColourChanged p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.NameColour = p.NameColour;
        }

        private void ObjectGuildNameChanged(S.ObjectGuildNameChanged p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob) && ob is PlayerObject obPlayer)
                obPlayer.GuildName = p.GuildName;
        }

        private void GainExperience(S.GainExperience p)
        {
            // 一句话显示：有 10倍经验点额外部分就一起带上，不拆两行刷屏。
            string text = p.ExtraExp > 0
                ? $"<color=lime>获得</color> 经验 x{p.Amount}，额外经验 x{p.ExtraExp}"
                : $"<color=lime>获得</color> 经验 x{p.Amount}";
            OutputMessage(text);
            MapObject.User.Experience += p.Amount + p.ExtraExp;
            MapObject.User.SExp = p.SExp;
        }

        private void GainHeroExperience(S.GainHeroExperience p)
        {
            OutputMessage($"<color=lime>获得</color> 英雄经验 x{p.Amount}");
            MapObject.Hero.Experience += p.Amount;
        }
        private void LevelChanged(S.LevelChanged p)
        {
            User.Level = p.Level;
            User.Experience = p.Experience;
            User.MaxExperience = p.MaxExperience;
            User.RefreshStats();
            // 简短提示："恭喜 升级到N级"，开头动词加色，其余默认白色+描边。
            OutputMessage($"<color=lime>恭喜</color> 升级到{p.Level}级");
            User.Effects.Add(new Effect(Libraries.Magic2, 1200, 20, 2000, User));
            SoundManager.PlaySound(SoundList.LevelUp);
        }
        private void HeroLevelChanged(S.HeroLevelChanged p)
        {
            Hero.Level = p.Level;
            Hero.Experience = p.Experience;
            Hero.MaxExperience = p.MaxExperience;
            Hero.RefreshStats();
            Hero.Effects.Add(new Effect(Libraries.Magic2, 1200, 20, 2000, User));
            SoundManager.PlaySound(SoundList.LevelUp);
            MainDialog.HeroInfoPanel.Update();
        }
        private void ObjectLeveled(S.ObjectLeveled p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                ob.Effects.Add(new Effect(Libraries.Magic2, 1180, 16, 2500, ob));
                SoundManager.PlaySound(SoundList.LevelUp);
            }
        }
        private void ObjectHarvest(S.ObjectHarvest p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Harvest, Direction = ob.Direction, Location = ob.CurrentLocation });
        }

        private void ObjectHarvested(S.ObjectHarvested p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Skeleton, Direction = ob.Direction, Location = ob.CurrentLocation });
        }

    }
}
