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
        private void NewMagic(S.NewMagic p)
        {
            ClientMagic magic = p.Magic;

            UserObject actor = User;
            if (p.Hero)
                actor = Hero;

            // 原始传奇技能：登录包已下发本职业全部技能（未学的 MaxLevel=0 占位）。
            // 升级解锁时服务端再次下发同 Spell，这里更新已有条目而非重复添加。
            ClientMagic existing = actor.Magics.Find(m => m.Spell == magic.Spell);
            if (existing != null)
            {
                existing.Level = magic.Level;
                existing.MaxLevel = magic.MaxLevel;
            }
            else
            {
                actor.Magics.Add(magic);
            }

            actor.RefreshStats();
            if (actor == User)
            {
                User.PreloadMagicEffects(existing ?? magic);
            }
            SkillBarDialogs.UpdateUI();
        }

        private void RemoveMagic(S.RemoveMagic p)
        {
            User.Magics.RemoveAt(p.PlaceId);
            User.RefreshStats();
            SkillBarDialogs.UpdateUI();
        }

        private void MagicLeveled(S.MagicLeveled p)
        {
            UserObject actor = p.ObjectID == Hero?.ObjectID ? Hero : User;

            for (int i = 0; i < actor.Magics.Count; i++)
            {
                ClientMagic magic = actor.Magics[i];
                if (magic.Spell != p.Spell) continue;

                if (magic.Level != p.Level)
                {
                    magic.Level = p.Level;
                    actor.RefreshStats();
                    if (actor == User)
                    {
                        User.PreloadMagicEffects(magic);
                    }
                }

                magic.Experience = p.Experience;
                break;
            }
        }
        private void Magic(S.Magic p)
        {
            User.Spell = p.Spell;
            User.Cast = p.Cast;
            User.TargetID = p.TargetID;
            User.TargetPoint = p.Target;
            User.SpellLevel = p.Level;
            User.SecondaryTargetIDs = p.SecondaryTargetIDs;

            // CD 起点由服务端施法成功回包驱动：只有 cast=true 才记 CastTime（与经典引擎一致）。
            // 技能因目标非法/未带符等原因未真正放出时（cast=false）不进 CD，玩家可立即重试，
            // 不会误弹"X秒内无法释放"；全局施法锁 SpellTime 仍会静默限制连发。
            if (!p.Cast) return;

            ClientMagic magic = User.GetMagic(p.Spell);
            if (magic != null)
                magic.CastTime = CMain.Time;
        }

        private void MagicDelay(S.MagicDelay p)
        {
            ClientMagic magic;
            if (p.ObjectID == Hero?.ObjectID)
                magic = Hero.GetMagic(p.Spell);
            else
                magic = User.GetMagic(p.Spell);
            magic.Delay = p.Delay;
        }

        private void MagicCast(S.MagicCast p)
        {
            ClientMagic magic = User.GetMagic(p.Spell);
            magic.CastTime = CMain.Time;
        }

        private void ObjectMagic(S.ObjectMagic p)
        {
            if (p.SelfBroadcast == false && p.ObjectID == User.ObjectID && !Observing) return;

            if (p.ObjectID == Hero?.ObjectID && p.Cast)
            {
                ClientMagic magic = Hero.GetMagic(p.Spell);
                magic.CastTime = CMain.Time;
            }

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                QueuedAction action = new QueuedAction { Action = MirAction.Spell, Direction = p.Direction, Location = p.Location, Params = new List<object>() };
                action.Params.Add(p.Spell);
                action.Params.Add(p.TargetID);
                action.Params.Add(p.Target);
                action.Params.Add(p.Cast);
                action.Params.Add(p.Level);
                action.Params.Add(p.SecondaryTargetIDs);

                ob.ActionFeed.Add(action);
            }
        }

        private void ObjectProjectile(S.ObjectProjectile p)
        {
            MapObject source = MapControl.GetObject(p.Source);

            if (source == null) return;

            // 原始传奇技能（值即 skillId）：飞行物回包驱动 CDN 特效（经典 .Lib Missile 已移除）。
            // 与经典 FireBounce 分支互斥——原始技能走这里，经典技能走下面 switch。
            // 自己施法的飞行物已由 PlayerObject.PlayOriginHitEffects 本地预测绘制（与原始传奇 JS 一致），
            // 这里跳过施法者自己的回包，避免双份子弹/命中；他人施法仍靠此回包绘制。
            if (source == User) return;

            int originId = (int)p.Spell;
            // ObjectProjectile 包不带技能等级，只能按 Lv1 取飞行物特效。
            if (originId > 218 && ExcelTableMgr.TryGetSkillEffects(originId, 1, out var steps))
            {
                SkillEffectTable flyStep = steps.FirstOrDefault(s => s.type == 2 && s.effect > 0);
                SkillEffectTable hitStep = steps.FirstOrDefault(s => s.type == 1 && s.effect > 0);
                MapObject target = MapControl.GetObject(p.Destination);

                if (flyStep != null)
                {
                    if (flyStep.sound > 0)
                        SoundManager.PlayOriginSound(flyStep.sound);
                    Point dest = target != null ? target.CurrentLocation : source.CurrentLocation;
                    // 飞行时长按经典 Missile.Process 口径：距离*50ms（与服务端 Fireball 的 delay 一致）。
                    // 不再读本地 skillEffects.delaytime——时序由经典逻辑决定，原始数据只用来选特效资源。
                    int flight = Math.Max(1, Functions.MaxDistance(source.CurrentLocation, dest) * 50);
                    // 与灵魂火符同款：子弹各用自己 model 的 stand，不做覆盖。
                    OriginMissile missile = new OriginMissile(flyStep.effect, source, target, dest,
                                                              flightDuration: flight);
                    if (target != null && hitStep != null)
                    {
                        int hitEffect = hitStep.effect, hitSound = hitStep.sound;
                        int hitX = hitStep.x, hitY = hitStep.y;
                        MapObject hitTarget = target;
                        missile.Complete += (o, e) =>
                        {
                            if (hitTarget.CurrentAction == MirAction.Dead) return;
                            // 与灵魂火符同款：命中特效不抬升(yOffset=0)，子弹终点已抬 60，两者错开才对。
                            hitTarget.Effects.Add(new OriginEffect(hitEffect, hitTarget,
                                xOffset: hitX, yOffset: -hitY));
                            if (hitSound > 0) SoundManager.PlaySound(hitSound);
                        };
                    }
                    source.Effects.Add(missile);
                }
                return;
            }

            switch (p.Spell)
            {
                case Spell.火弹跳: // 火焰弹
                    {
                        SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10 + 1);

                        Missile missile = source.CreateProjectile(410, Libraries.Magic, true, 6, 30, 4, targetID: p.Destination);

                        if (missile.Target != null)
                        {
                            missile.Complete += (o, e) =>
                            {
                                var sender = (Missile)o;

                                if (sender.Target.CurrentAction == MirAction.Dead) return;
                                sender.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, sender.Target));
                                SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10 + 2);
                            };
                        }
                    }
                    break;
            }
        }

        private void ObjectEffect(S.ObjectEffect p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                PlayerObject player;

                switch (p.Effect)
                {
                    case SpellEffect.FurbolgWarriorCritical:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.FurbolgWarrior], 400, 6, 600, ob));
                        SoundManager.PlaySound(20000 + (ushort)Spell.致命剑 * 10);
                        break;
                    case SpellEffect.FatalSword:
                        ob.Effects.Add(new Effect(Libraries.Magic2, 1940, 4, 400, ob));
                        SoundManager.PlaySound(20000 + (ushort)Spell.致命剑 * 10);
                        break;
                    case SpellEffect.StormEscape:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 610, 10, 600, ob));
                        SoundManager.PlaySound(SoundList.Teleport);
                        break;
                    case SpellEffect.Teleport:
                        ob.Effects.Add(new Effect(Libraries.Magic, 1600, 10, 600, ob));
                        SoundManager.PlaySound(SoundList.Teleport);
                        break;
                    case SpellEffect.Healing:
                        SoundManager.PlaySound(20000 + (ushort)Spell.治愈术 * 10 + 1);
                        ob.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, ob));
                        break;
                    case SpellEffect.RedMoonEvil:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.RedMoonEvil], 32, 6, 400, ob) { Blend = false });
                        break;
                    case SpellEffect.TwinDrakeBlade:
                        ob.Effects.Add(new Effect(Libraries.Magic2, 380, 6, 800, ob));
                        break;
                    case SpellEffect.MPEater:
                        if (MapControl.Objects.TryGetValue(p.EffectType, out var ob2))
                            ob2.Effects.Add(new Effect(Libraries.Magic2, 2411, 19, 1900, ob2));

                        ob.Effects.Add(new Effect(Libraries.Magic2, 2400, 9, 900, ob));
                        SoundManager.PlaySound(20000 + (ushort)Spell.致命剑 * 10);
                        break;
                    case SpellEffect.Bleeding:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 60, 3, 400, ob));
                        break;
                    case SpellEffect.Hemorrhage:
                        SoundManager.PlaySound(20000 + (ushort)Spell.出血 * 10);
                        ob.Effects.Add(new Effect(Libraries.Magic3, 0, 4, 400, ob));
                        ob.Effects.Add(new Effect(Libraries.Magic3, 28, 6, 600, ob));
                        ob.Effects.Add(new Effect(Libraries.Magic3, 46, 8, 800, ob));
                        break;
                    case SpellEffect.MagicShieldUp:
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Hero) return;
                        player = (PlayerObject)ob;
                        if (player.ShieldEffect != null)
                        {
                            player.ShieldEffect.Clear();
                            player.ShieldEffect.Remove();
                        }
                        player.MagicShield = true;
                        player.Effects.Add(player.ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, ob) { Repeat = true });
                        break;
                    case SpellEffect.MagicShieldDown:
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Hero) return;
                        player = (PlayerObject)ob;
                        if (player.ShieldEffect != null)
                        {
                            player.ShieldEffect.Clear();
                            player.ShieldEffect.Remove();
                        }
                        player.ShieldEffect = null;
                        player.MagicShield = false;
                        break;
                    case SpellEffect.GreatFoxSpirit:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.GreatFoxSpirit], 375 + (CMain.Random.Next(3) * 20), 20, 1400, ob));
                        SoundManager.PlaySound(((ushort)Monster.GreatFoxSpirit * 10) + 5);
                        break;
                    case SpellEffect.Entrapment:
                        ob.Effects.Add(new Effect(Libraries.Magic2, 1010, 10, 1500, ob));
                        ob.Effects.Add(new Effect(Libraries.Magic2, 1020, 8, 1200, ob));
                        break;
                    case SpellEffect.Critical:
                        //ob.Effects.Add(new Effect(Libraries.CustomEffects, 0, 12, 60, ob));
                        break;
                    case SpellEffect.Reflect:
                        ob.Effects.Add(new Effect(Libraries.Effect, 580, 10, 70, ob));
                        break;
                    case SpellEffect.ElementalBarrierUp:
                        if (ob.Race != ObjectType.Player) return;
                        player = (PlayerObject)ob;
                        if (player.ElementalBarrierEffect != null)
                        {
                            player.ElementalBarrierEffect.Clear();
                            player.ElementalBarrierEffect.Remove();
                        }

                        player.ElementalBarrier = true;
                        player.Effects.Add(player.ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 10, 2000, ob) { Repeat = true });
                        break;
                    case SpellEffect.ElementalBarrierDown:
                        if (ob.Race != ObjectType.Player) return;
                        player = (PlayerObject)ob;
                        if (player.ElementalBarrierEffect != null)
                        {
                            player.ElementalBarrierEffect.Clear();
                            player.ElementalBarrierEffect.Remove();
                        }
                        player.ElementalBarrierEffect = null;
                        player.ElementalBarrier = false;
                        player.Effects.Add(player.ElementalBarrierEffect = new Effect(Libraries.Magic3, 1910, 7, 1400, ob));
                        SoundManager.PlaySound(20000 + 131 * 10 + 5);
                        break;
                    case SpellEffect.DelayedExplosion:
                        int effectid = DelayedExplosionEffect.GetOwnerEffectID(ob.ObjectID);
                        if (effectid < 0)
                        {
                            ob.Effects.Add(new DelayedExplosionEffect(Libraries.Magic3, 1590, 8, 1200, ob, true, 0, 0));
                        }
                        else if (effectid >= 0)
                        {
                            if (DelayedExplosionEffect.effectlist[effectid].stage < p.EffectType)
                            {
                                DelayedExplosionEffect.effectlist[effectid].Remove();
                                ob.Effects.Add(new DelayedExplosionEffect(Libraries.Magic3, 1590 + ((int)p.EffectType * 10), 8, 1200, ob, true, (int)p.EffectType, 0));
                            }
                        }
                        break;
                    case SpellEffect.AwakeningSuccess:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 900, 16, 1600, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50002);
                            ef.Complete += (o, e) => MapControl.AwakeningAction = false;
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 840, 16, 1600, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.AwakeningFail:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 920, 9, 900, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50003);
                            ef.Complete += (o, e) => MapControl.AwakeningAction = false;
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 860, 9, 900, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.AwakeningHit:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 880, 5, 500, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50001);
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 820, 5, 500, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.AwakeningMiss:
                        {
                            Effect ef = new Effect(Libraries.Magic3, 890, 5, 500, ob, CMain.Time + p.DelayTime);
                            ef.Played += (o, e) => SoundManager.PlaySound(50000);
                            ob.Effects.Add(ef);
                            ob.Effects.Add(new Effect(Libraries.Magic3, 830, 5, 500, ob, CMain.Time + p.DelayTime) { Blend = false });
                        }
                        break;
                    case SpellEffect.TurtleKing:
                        {
                            Effect ef = new Effect(Libraries.Monsters[(ushort)Monster.TurtleKing], CMain.Random.Next(2) == 0 ? 922 : 934, 12, 1200, ob);
                            ef.Played += (o, e) => SoundManager.PlaySound(20000 + (ushort)Spell.地狱火 * 10 + 1);
                            ob.Effects.Add(ef);
                        }
                        break;
                    case SpellEffect.Behemoth:
                        {
                            MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Behemoth], 788, 10, 1500, ob.CurrentLocation));
                            MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Behemoth], 778, 10, 1500, ob.CurrentLocation, 0, true) { Blend = false });
                        }
                        break;
                    case SpellEffect.Stunned:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.StoningStatue], 632, 10, 1000, ob)
                        {
                            Repeat = p.Time > 0,
                            RepeatUntil = p.Time > 0 ? CMain.Time + p.Time : 0
                        });
                        break;
                    case SpellEffect.IcePillar:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.IcePillar], 18, 8, 800, ob));
                        break;
                    case SpellEffect.KingGuard:
                        if (p.EffectType == 0)
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.KingGuard], 753, 10, 1000, ob) { Blend = false });
                        }
                        else
                        {
                            ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.KingGuard], 763, 10, 1000, ob) { Blend = false });
                        }
                        break;
                    case SpellEffect.FlamingMutantWeb:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.FlamingMutant], 330, 10, 1000, ob)
                        {
                            Repeat = p.Time > 0,
                            RepeatUntil = p.Time > 0 ? CMain.Time + p.Time : 0
                        });
                        break;
                    case SpellEffect.DeathCrawlerBreath:
                        ob.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.DeathCrawler], 272 + ((int)ob.Direction * 4), 4, 400, ob) { Blend = true });
                        break;
                    case SpellEffect.MoonMist:
                        ob.Effects.Add(new Effect(Libraries.Magic3, 705, 10, 800, ob));
                        break;
                }
            }
        }

        private void RangeAttack(S.RangeAttack p)
        {
            User.TargetID = p.TargetID;
            User.TargetPoint = p.Target;
            User.Spell = p.Spell;
        }

        private void Pushed(S.Pushed p)
        {
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.Pushed, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectPushed(S.ObjectPushed p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Pushed, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectName(S.ObjectName p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.Name = p.Name;
        }

        private void UserStorage(S.UserStorage p)
        {
            if (Storage.Length != p.Storage.Length)
            {
                Array.Resize(ref Storage, p.Storage.Length);
            }

            Storage = p.Storage;

            for (int i = 0; i < Storage.Length; i++)
            {
                if (Storage[i] == null) continue;
                Bind(Storage[i]);
            }
        }
        private void SwitchGroup(S.SwitchGroup p)
        {
            GroupDialog.AllowGroup = p.AllowGroup;

            if (!p.AllowGroup && GroupDialog.GroupList.Count > 0)
                DeleteGroup();
        }

        private void DeleteGroup()
        {
            GroupDialog.GroupList.Clear();
            GroupDialog.GroupMembersMap.Clear();
            BigMapViewPort.PlayerLocations.Clear();
            ChatDialog.ReceiveChat(LanguageMgr.Get("YouHaveLeftGroup"), ChatType.Team);
        }

        private void DeleteMember(S.DeleteMember p)
        {
            GroupDialog.GroupList.Remove(p.Name);
            GroupDialog.GroupMembersMap.Remove(p.Name);
            BigMapViewPort.PlayerLocations.Remove(p.Name);
            ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerHasLeftGroup", p.Name), ChatType.Team);
        }

        private void GroupInvite(S.GroupInvite p)
        {
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("DoYouWantGroupWithPlayer", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.GroupInvite { AcceptInvite = true });
                GroupDialog.Show();
            };
            messageBox.NoButton.Click += (o, e) => Network.Enqueue(new C.GroupInvite { AcceptInvite = false });
            messageBox.Show();
        }
        private void AddMember(S.AddMember p)
        {
            GroupDialog.GroupList.Add(p.Name);
            ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerHasJoinedGroup", p.Name), ChatType.Team);
        }
        private void GroupMembersMap(S.GroupMembersMap p)
        {
            if (!GroupDialog.GroupMembersMap.ContainsKey(p.PlayerName))
                GroupDialog.GroupMembersMap.Add(p.PlayerName, p.PlayerMap);
            else
            {
                GroupDialog.GroupMembersMap.Remove(p.PlayerName);
                GroupDialog.GroupMembersMap.Add(p.PlayerName, p.PlayerMap);
            }
        }
        private void SendMemberLocation(S.SendMemberLocation p)
        {
            if (!BigMapViewPort.PlayerLocations.ContainsKey(p.MemberName))
                BigMapViewPort.PlayerLocations.Add(p.MemberName, p.MemberLocation);
            else
            {
                BigMapViewPort.PlayerLocations.Remove(p.MemberName);
                BigMapViewPort.PlayerLocations.Add(p.MemberName, p.MemberLocation);
            }
        }
        private void Revived()
        {
            User.SetAction();
            User.Dead = false;
            User.Effects.Add(new Effect(Libraries.Magic2, 1220, 20, 2000, User));
            SoundManager.PlaySound(SoundList.Revive);
        }
        private void ObjectRevived(S.ObjectRevived p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                if (p.Effect)
                {
                    ob.Effects.Add(new Effect(Libraries.Magic2, 1220, 20, 2000, ob));
                    SoundManager.PlaySound(SoundList.Revive);
                }
                ob.Dead = false;
                ob.ActionFeed.Clear();
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Revive, Direction = ob.Direction, Location = ob.CurrentLocation });
            }
        }
        private void SpellToggle(S.SpellToggle p)
        {
            UserObject actor = User;
            string prefix = string.Empty;

            if (p.ObjectID == Hero?.ObjectID)
            {
                actor = Hero;
                prefix = LanguageMgr.Get("Hero");
            }

            switch (p.Spell)
            {
                //Warrior
                case Spell.攻杀剑术: // 攻杀剑术
                    actor.Slaying = p.CanUse;
                    break;
                case Spell.刺杀剑术: // 刺杀剑术
                    actor.Thrusting = p.CanUse;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.Thrusting ? LanguageMgr.Get("UseThrusting"): LanguageMgr.Get("DoNotUseThrusting")),
                        ChatType.System);
                    break;
                case Spell.半月弯刀: // 半月弯刀
                    actor.HalfMoon = p.CanUse;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.HalfMoon ? LanguageMgr.Get("UseHalfMoon"): LanguageMgr.Get("DoNotUseHalfMoon")),
                        ChatType.System);
                    break;
                case Spell.双半月: // 半月弯刀（双）
                    actor.CrossHalfMoon = p.CanUse;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.CrossHalfMoon ? LanguageMgr.Get("UseCrossHalfMoon"): LanguageMgr.Get("DoNotUseCrossHalfMoon")),
                        ChatType.System);
                    break;
                case Spell.双斩: // 双斩
                    actor.DoubleSlash = p.CanUse;
                    ChatDialog.ReceiveChat(
                        prefix + (actor.DoubleSlash ? LanguageMgr.Get("UseDoubleSlash"): LanguageMgr.Get("DoNotUseDoubleSlash")),
                        ChatType.System);
                    break;
                case Spell.烈火剑法: // 烈火剑法
                    actor.FlamingSword = p.CanUse;
                    if (actor.FlamingSword)
                        ChatDialog.ReceiveChat(prefix + LanguageMgr.Get("WeaponSpiritFire"), ChatType.System);
                    else
                        ChatDialog.ReceiveChat(prefix + LanguageMgr.Get("SpiritsFireDisappeared"), ChatType.System);
                    break;
            }
        }

        private void ObjectHealth(S.ObjectHealth p)
        {
            if (p.ObjectID == Hero?.ObjectID)
            {
                Hero.PercentHealth = p.Percent;
                Hero.HP = p.HP; Hero.MaxHP = p.MaxHP;
            }

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                ob.PercentHealth = p.Percent;
                ob.HealthTime = CMain.Time + p.Expire * 1000;
                ob.HP = p.HP; ob.MaxHP = p.MaxHP;
            }
        }

        private void ObjectMana(S.ObjectMana p)
        {
            if (p.ObjectID == Hero?.ObjectID)
                Hero.PercentMana = p.Percent;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.PercentMana = p.Percent;
        }


        private void MapEffect(S.MapEffect p)
        {
            switch (p.Effect)
            {
                case SpellEffect.Mine:
                    SoundManager.PlaySound(10091);
                    Effect HitWall = new Effect(Libraries.Effect, 8 * p.Value, 3, 240, p.Location) { Light = 0 };
                    MapControl.Effects.Add(HitWall);
                    break;
                case SpellEffect.Tester:
                    Effect eff = new Effect(Libraries.Effect, 328, 10, 500, p.Location) { Light = 0 };
                    MapControl.Effects.Add(eff);
                    break;
            }
        }

        private void ObjectRangeAttack(S.ObjectRangeAttack p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                QueuedAction action = null;
                if (ob.Race == ObjectType.Player)
                {
                    action = p.Type switch
                    {
                        _ => new QueuedAction { Action = MirAction.AttackRange1, Direction = p.Direction, Location = p.Location, Params = new List<object>() }
                    };
                }
                else
                {
                    action = p.Type switch
                    {
                        1 => new QueuedAction { Action = MirAction.AttackRange2, Direction = p.Direction, Location = p.Location, Params = new List<object>() },
                        2 => new QueuedAction { Action = MirAction.AttackRange3, Direction = p.Direction, Location = p.Location, Params = new List<object>() },
                        _ => new QueuedAction { Action = MirAction.AttackRange1, Direction = p.Direction, Location = p.Location, Params = new List<object>() }
                    };
                }

                action.Params.Add(p.TargetID);
                action.Params.Add(p.Target);
                action.Params.Add(p.Spell);
                action.Params.Add(new List<uint>());
                action.Params.Add(p.Level);

                ob.ActionFeed.Add(action);
            }
        }

        private void AddBuff(S.AddBuff p)
        {
            ClientBuff buff = p.Buff;

            if (!buff.Paused)
            {
                buff.ExpireTime += CMain.Time;
            }

            if (buff.ObjectID == User.ObjectID)
            {
                for (int i = 0; i < BuffsDialog.Buffs.Count; i++)
                {
                    if (BuffsDialog.Buffs[i].Type != buff.Type) continue;

                    BuffsDialog.Buffs[i] = buff;
                    User.RefreshStats();
                    return;
                }

                BuffsDialog.Buffs.Add(buff);
                BuffsDialog.CreateBuff(buff);

                User.RefreshStats();
            }

            if (!buff.Visible || buff.ObjectID <= 0) return;

            if (MapControl.Objects.TryGetValue(buff.ObjectID, out var ob) && (ob is PlayerObject || ob is MonsterObject))
            {
                if (!ob.Buffs.Contains(buff.Type))
                    ob.Buffs.Add(buff.Type);

                // 资源库按需加载：对应特效库缺失/未就绪时（如 Libraries.CHumEffect[5] 越界），
                // 只跳过该 buff 的特效表现，不能让整个收包流程因 IndexOutOfRange 崩掉。
                try
                {
                    ob.AddBuffEffect(buff.Type);
                }
                catch (Exception ex)
                {
                    //KFramework.MonoGame.PrintTool.Log("[Buff] 添加 buff 特效失败（资源缺失）: " + buff.Type + " -> " + ex.Message);
                }
            }
        }

        private void RemoveBuff(S.RemoveBuff p)
        {
            if (User.ObjectID == p.ObjectID)
            {
                for (int i = 0; i < BuffsDialog.Buffs.Count; i++)
                {
                    if (BuffsDialog.Buffs[i].Type != p.Type) continue;

                    switch (BuffsDialog.Buffs[i].Type)
                    {
                        case BuffType.SwiftFeet:
                            User.Sprint = false;
                            break;
                        case BuffType.Transform:
                            User.TransformType = -1;
                            break;
                    }

                    BuffsDialog.RemoveBuff(i);
                    BuffsDialog.Buffs.RemoveAt(i);
                }
                User.RefreshStats();
            }

            if (p.ObjectID <= 0) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                ob.Buffs.Remove(p.Type);
                ob.RemoveBuffEffect(p.Type);
            }
        }

        private void PauseBuff(S.PauseBuff p)
        {
            if (User.ObjectID == p.ObjectID)
            {
                for (int i = 0; i < BuffsDialog.Buffs.Count; i++)
                {
                    if (BuffsDialog.Buffs[i].Type != p.Type) continue;

                    User.RefreshStats();

                    if (BuffsDialog.Buffs[i].Paused == p.Paused) return;

                    BuffsDialog.Buffs[i].Paused = p.Paused;

                    if (p.Paused)
                    {
                        BuffsDialog.Buffs[i].ExpireTime -= CMain.Time;
                    }
                    else
                    {
                        BuffsDialog.Buffs[i].ExpireTime += CMain.Time;
                    }
                }
            }

        }

        private void ObjectHidden(S.ObjectHidden p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.Hidden = p.Hidden;
        }

        private void ObjectSneaking(S.ObjectSneaking p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                // ob.SneakingActive = p.SneakingActive;
            }
        }

        private void ObjectLevelEffects(S.ObjectLevelEffects p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob) && ob is PlayerObject player)
            {
                player.LevelEffects = p.LevelEffects;
                player.SetEffects();
            }
        }

        private void RefreshItem(S.RefreshItem p)
        {
            Bind(p.Item);

            if (SelectedCell != null && SelectedCell.Item.UniqueID == p.Item.UniqueID)
                SelectedCell = null;

            if (HoverItem != null && HoverItem.UniqueID == p.Item.UniqueID)
            {
                DisposeItemLabel();
                CreateItemLabel(p.Item);
            }

            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null && User.Inventory[i].UniqueID == p.Item.UniqueID)
                {
                    User.Inventory[i] = p.Item;
                    User.RefreshStats();
                    return;
                }
            }

            for (int i = 0; i < User.Equipment.Length; i++)
            {
                if (User.Equipment[i] != null && User.Equipment[i].UniqueID == p.Item.UniqueID)
                {
                    User.Equipment[i] = p.Item;
                    User.RefreshStats();
                    return;
                }
            }

            if (Hero != null)
            {
                for (int i = 0; i < Hero.Inventory.Length; i++)
                {
                    if (Hero.Inventory[i] != null && Hero.Inventory[i].UniqueID == p.Item.UniqueID)
                    {
                        Hero.Inventory[i] = p.Item;
                        Hero.RefreshStats();
                        return;
                    }
                }

                for (int i = 0; i < Hero.Equipment.Length; i++)
                {
                    if (Hero.Equipment[i] != null && Hero.Equipment[i].UniqueID == p.Item.UniqueID)
                    {
                        Hero.Equipment[i] = p.Item;
                        Hero.RefreshStats();
                        return;
                    }
                }
            }
        }

        private void ObjectSpell(S.ObjectSpell p)
        {
            SpellObject ob = new SpellObject(p.ObjectID);
            ob.Load(p);
        }

        private void ObjectDeco(S.ObjectDeco p)
        {
            DecoObject ob = new DecoObject(p.ObjectID);
            ob.Load(p);
        }

        private void UserDash(S.UserDash p)
        {
            if (User.Direction == p.Direction && User.CurrentLocation == p.Location)
            {
                MapControl.NextAction = 0;
                return;
            }
            MirAction action = User.CurrentAction == MirAction.DashL ? MirAction.DashR : MirAction.DashL;
            for (int i = User.ActionFeed.Count - 1; i >= 0; i--)
            {
                if (User.ActionFeed[i].Action == MirAction.DashR)
                {
                    action = MirAction.DashL;
                    break;
                }
                if (User.ActionFeed[i].Action == MirAction.DashL)
                {
                    action = MirAction.DashR;
                    break;
                }
            }

            User.ActionFeed.Add(new QueuedAction { Action = action, Direction = p.Direction, Location = p.Location });
        }

        private void UserDashFail(S.UserDashFail p)
        {
            MapControl.NextAction = 0;
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.DashFail, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectDash(S.ObjectDash p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                var action = ob.ActionFeed.Count > 0 && ob.ActionFeed[^1].Action == MirAction.DashL ? MirAction.DashR : MirAction.DashL;
                ob.ActionFeed.Add(new QueuedAction { Action = action, Direction = p.Direction, Location = p.Location });
            }

        }

        private void ObjectDashFail(S.ObjectDashFail p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.DashFail, Direction = p.Direction, Location = p.Location });
        }

        private void UserBackStep(S.UserBackStep p)
        {
            if (User.Direction == p.Direction && User.CurrentLocation == p.Location)
            {
                MapControl.NextAction = 0;
                return;
            }
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.Jump, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectBackStep(S.ObjectBackStep p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                ob.JumpDistance = p.Distance;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Jump, Direction = p.Direction, Location = p.Location });
            }
        }

        private void UserDashAttack(S.UserDashAttack p)
        {
            if (User.Direction == p.Direction && User.CurrentLocation == p.Location)
            {
                MapControl.NextAction = 0;
                return;
            }
            //User.JumpDistance = p.Distance;
            User.ActionFeed.Add(new QueuedAction { Action = MirAction.DashAttack, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectDashAttack(S.ObjectDashAttack p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob))
            {
                ob.JumpDistance = p.Distance;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.DashAttack, Direction = p.Direction, Location = p.Location });
            }
        }

        private void UserAttackMove(S.UserAttackMove p)//Warrior Skill - SlashingBurst
        {
            MapControl.NextAction = 0;
            if (User.CurrentLocation == p.Location && User.Direction == p.Direction) return;


            MapControl.RemoveObject(User);
            User.CurrentLocation = p.Location;
            User.MapLocation = p.Location;
            MapControl.AddObject(User);


            MapControl.FloorValid = false;
            MapControl.InputDelay = CMain.Time + 400;


            if (User.Dead) return;


            User.ClearMagic();
            User.QueuedAction = null;


            for (int i = User.ActionFeed.Count - 1; i >= 0; i--)
            {
                if (User.ActionFeed[i].Action == MirAction.Pushed) continue;
                User.ActionFeed.RemoveAt(i);
            }


            User.SetAction();

            User.ActionFeed.Add(new QueuedAction { Action = MirAction.Standing, Direction = p.Direction, Location = p.Location });
        }

        private void SetConcentration(S.SetConcentration p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob) && (ob.Race == ObjectType.Player || ob.Race == ObjectType.Hero))
            {
                var player = (PlayerObject)ob;
                player.Concentrating = p.Enabled;
                player.ConcentrateInterrupted = p.Interrupted;

                if (p.Enabled && !p.Interrupted && InterruptionEffect.GetOwnerEffectID(player.ObjectID) < 0)
                {
                    player.Effects.Add(new InterruptionEffect(Libraries.Magic3, 1860, 8, 8 * 100, player, true));
                    SoundManager.PlaySound(20000 + 129 * 10);
                }
            }
        }

        private void SetElemental(S.SetElemental p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob) && (ob.Race == ObjectType.Player || ob.Race == ObjectType.Hero))
            {
                var player = (PlayerObject)ob;
                player.HasElements = p.Enabled;
                player.ElementCasted = p.Casted && User.ObjectID != p.ObjectID;
                player.ElementsLevel = (int)p.Value;

                if (p.Enabled && p.ElementType > 0)
                {
                    int elementType = (int)p.ElementType;
                    int maxExp = (int)p.ExpLast;
                    player.Effects.Add(new ElementsEffect(Libraries.Magic3, 1630 + ((elementType - 1) * 10), 10, 10 * 100, player, true, 1 + (elementType - 1), maxExp, User.ObjectID == p.ObjectID && (elementType == 4 || elementType == 3)));
                }
            }
        }

        private void RemoveDelayedExplosion(S.RemoveDelayedExplosion p)
        {
            //if (p.ObjectID == User.ObjectID) return;

            int effectid = DelayedExplosionEffect.GetOwnerEffectID(p.ObjectID);
            if (effectid >= 0)
                DelayedExplosionEffect.effectlist[effectid].Remove();
        }
        private void SetBindingShot(S.SetBindingShot p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob) && ob.Race == ObjectType.Monster)
            {
                var NetCast = new TrackableEffect(new Effect(Libraries.MagicC, 0, 8, 700, ob)) { EffectName = "BindingShotDrop" };
                var NetDropped = new TrackableEffect(new Effect(Libraries.MagicC, 7, 1, 1000, ob) { Repeat = true, RepeatUntil = CMain.Time + (p.Value - 1500) }) { EffectName = "BindingShotDown" };
                var NetFall = new TrackableEffect(new Effect(Libraries.MagicC, 8, 8, 700, ob)) { EffectName = "BindingShotFall" };

                NetDropped.Complete += (o1, e1) =>
                {
                    SoundManager.PlaySound(20000 + 130 * 10 + 6); // sound M130-6
                    ob.Effects.Add(NetFall);
                };

                NetCast.Complete += (o, e) =>
                {
                    SoundManager.PlaySound(20000 + 130 * 10 + 5); // sound M130-5
                    ob.Effects.Add(NetDropped);
                };

                ob.Effects.Add(NetCast);
            }
        }

    }
}
