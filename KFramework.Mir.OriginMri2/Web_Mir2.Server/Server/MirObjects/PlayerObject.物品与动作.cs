// partial 文件：PlayerObject 功能拆分（原 PlayerObject.cs 原样迁入）。
using System.Drawing;
using C = ClientPackets;
using Server.ExcelConfig;
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
        public void Turn(MirDirection dir)
        {
            _stepCounter = 0;

            if (CanMove)
            {
                ActionTime = Envir.Time + GetDelayTime(TurnDelay);

                Direction = dir;
                if (CheckMovement(CurrentLocation)) return;

                SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

                if (szi != null)
                {
                    if (szi.StartPoint)
                    {
                        BindLocation = szi.Location;
                        BindMapIndex = CurrentMapIndex;
                    }
                    InSafeZone = true;   // 任意安全区都提供庇护（含矿区入口），避免一进图就被怪打死
                }
                else
                    InSafeZone = false;

                Cell cell = CurrentMap.GetCell(CurrentLocation);

                if (cell != null && cell.Objects != null)
                {
                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        if (cell.Objects[i].Race != ObjectType.Spell) continue;
                        SpellObject ob = (SpellObject)cell.Objects[i];

                        ob.ProcessSpell(this);

                    }
                }

                if (TradePartner != null)
                    TradeCancel();

                if (ItemRentalPartner != null)
                    CancelItemRental();

                Broadcast(new S.ObjectTurn { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
            }

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
        }
        public void Harvest(MirDirection dir)
        {
            if (!CanMove)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            ActionTime = Envir.Time + HarvestDelay;

            Direction = dir;

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectHarvest { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            Point front = Front;
            bool send = false;
            for (int d = 0; d <= 1; d++)
            {
                for (int y = front.Y - d; y <= front.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= CurrentMap.Height) break;

                    for (int x = front.X - d; x <= front.X + d; x += Math.Abs(y - front.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= CurrentMap.Width) break;
                        if (!CurrentMap.ValidPoint(x, y)) continue;

                        Cell cell = CurrentMap.GetCell(x, y);
                        if (cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Race != ObjectType.Monster || !ob.Dead || ob.Harvested) continue;

                            if (ob.EXPOwner != null && ob.EXPOwner != this && !IsMember(ob))
                            {
                                send = true;
                                continue;
                            }

                            if (ob.Harvest(this)) return;
                        }
                    }
                }
            }

            if (send)
                ReceiveChat(LanguageMgr.Get("NoNearbyOwnedCarcasses"), ChatType.System);
        }
        private void CompleteNPC(IList<object> data)
        {
            uint npcid = (uint)data[0];
            int scriptid = (int)data[1];
            string page = (string)data[2];

            if (data.Count == 5)
            {
                Map map = (Map)data[3];
                Point coords = (Point)data[4];

                Teleport(map, coords);
            }

            NPCDelayed = true;

            if (page.Length > 0)
            {
                var script = NPCScript.Get(scriptid);
                script.Call(this, npcid, page.ToUpper());
            }
        }
        private UserItem GetBait(int count)
        {
            UserItem item = Info.Equipment[(int)EquipmentSlot.Weapon];
            if (item == null || item.Info.Type != ItemType.Weapon || !item.Info.IsFishingRod) return null;

            UserItem bait = item.Slots[(int)FishingSlot.Bait];

            if (bait == null || bait.Count < count) return null;

            return bait;
        }
        private UserItem GetFishingItem(FishingSlot type)
        {
            UserItem item = Info.Equipment[(int)EquipmentSlot.Weapon];
            if (item == null || item.Info.Type != ItemType.Weapon || !item.Info.IsFishingRod) return null;

            UserItem fishingItem = item.Slots[(int)type];

            if (fishingItem == null) return null;

            return fishingItem;
        }
        private void DeleteFishingItem(FishingSlot type)
        {
            UserItem item = Info.Equipment[(int)EquipmentSlot.Weapon];
            if (item == null || item.Info.Type != ItemType.Weapon || !item.Info.IsFishingRod) return;

            UserItem slotItem = Info.Equipment[(int)EquipmentSlot.Weapon].Slots[(int)type];

            Enqueue(new S.DeleteItem { UniqueID = slotItem.UniqueID, Count = 1 });
            Info.Equipment[(int)EquipmentSlot.Weapon].Slots[(int)type] = null;

            Report.ItemChanged(slotItem, 1, 1);
        }
        private void DamagedFishingItem(FishingSlot type, int lossDura)
        {
            UserItem item = GetFishingItem(type);

            if (item != null)
            {
                if (item.CurrentDura <= 0)
                {

                    DeleteFishingItem(type);
                }
                else
                {
                    DamageItem(item, lossDura, true);
                }
            }
        }
        public override bool CheckMovement(Point location)
        {
            if (Envir.Time < MovementTime) return false;

            //Script triggered coords
            for (int s = 0; s < CurrentMap.Info.ActiveCoords.Count; s++)
            {
                Point activeCoord = CurrentMap.Info.ActiveCoords[s];

                if (activeCoord != location) continue;

                CallDefaultNPC(DefaultNPCType.MapCoord, CurrentMap.Info.FileName, activeCoord.X, activeCoord.Y);
            }

            //Map movements
            for (int i = 0; i < CurrentMap.Info.Movements.Count; i++)
            {
                MovementInfo info = CurrentMap.Info.Movements[i];

                if (info.Source != location) continue;

                if (info.NeedHole)
                {
                    Cell cell = CurrentMap.GetCell(location);

                    if (cell.Objects == null ||
                        cell.Objects.Where(ob => ob.Race == ObjectType.Spell).All(ob => ((SpellObject)ob).Spell != Spell.挖地僵尸 && ((SpellObject)ob).Spell != Spell.挖地穿山甲))
                        continue;
                }

                if (info.ConquestIndex > 0)
                {
                    if (MyGuild == null || MyGuild.Conquest == null) continue;
                    if (MyGuild.Conquest.Info.Index != info.ConquestIndex) continue;
                }

                if (info.NeedMove) //use with ENTERMAP npc command
                {
                    NPCData["NPCMoveMap"] = Envir.GetMap(info.MapIndex);
                    NPCData["NPCMoveCoord"] = info.Destination;
                    continue;
                }

                Map temp = Envir.GetMap(info.MapIndex);

                if (temp == null || !temp.ValidPoint(info.Destination)) continue;

                CurrentMap.RemoveObject(this);
                Broadcast(new S.ObjectRemove { ObjectID = ObjectID });

                CompleteMapMovement(temp, info.Destination, CurrentMap, CurrentLocation);
                return true;
            }

            return false;
        }
        private void CompleteMapMovement(params object[] data)
        {
            if (this == null) return;

            Map temp = (Map)data[0];
            Point destination = (Point)data[1];
            Map checkmap = (Map)data[2];
            Point checklocation = (Point)data[3];

            if (CurrentMap != checkmap || CurrentLocation != checklocation) return;

            // --- RG pre-gate (deny before leaving source) ---
            if (temp.Info.RequiredGroup && !IsGM)
            {
                int required = Math.Max(2, temp.Info.RequiredGroupSize);
                int have = GroupMembers?.Count ?? 0;
                if (have < required)
                {
                    ReceiveChat(LanguageMgr.Get("MustBeInGroupMembers", required), ChatType.System);
                    return; // stay on source
                }
            }

            // Remember last safe only when dest is non-RG
            if (!temp.Info.RequiredGroup)
            {
                LastValidMap = temp;
                LastValidLocation = destination;
            }

            bool mapChanged = temp != CurrentMap;

            // --- Detach from source AFTER gate (no broadcast here) ---
            if (checkmap != null && checkmap.ValidPoint(checklocation))
            {
                var srcCell = checkmap.GetCell(checklocation);
                if (srcCell != null && srcCell.Objects != null && srcCell.Objects.Contains(this))
                {
                    checkmap.RemoveObject(this);
                }
            }

            // Switch to destination
            CurrentMap = temp;
            CurrentLocation = destination;
            CurrentMapIndex = temp.Info.Index;

            CurrentMap.AddObject(this);

            MovementTime = Envir.Time + MovementDelay;

            Enqueue(new S.MapChanged
            {
                MapIndex = CurrentMap.Info.Index,
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                Weather = CurrentMap.Info.WeatherParticles,
                MiniMap = CurrentMap.Info.MiniMap,
                BigMap = CurrentMap.Info.BigMap,
                Lights = CurrentMap.Info.Light,
                Location = CurrentLocation,
                Direction = Direction,
                MapDarkLight = CurrentMap.Info.MapDarkLight,
                Music = CurrentMap.Info.Music
            });

            if (RidingMount) RefreshMount();

            GetObjects();

            // Safe-zone / bind update（仅城镇/出生点安全区 StartPoint 更新绑定，避免覆盖回城点）
            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);
            if (szi != null)
            {
                if (szi.StartPoint)
                {
                    BindLocation = szi.Location;
                    BindMapIndex = CurrentMapIndex;
                }
                InSafeZone = true;   // 任意安全区都提供庇护（含矿区入口），避免一进图就被怪打死
            }
            else InSafeZone = false;

            // Entry rules (includes NoPets)
            if (mapChanged)
            {
                ApplyMapEntryRules(mapChanged);
            }

            GetPlayerLocation();

            // --- RG immediate enforcement on landing ---
            if (MapHasGroupRequirement(CurrentMap) && !IsValidForGroupRequiredMap())
            {
                ForceLeaveGroupRequiredMap();
            }

            // Relationship & conquest hooks
            if (Info.Married != 0)
            {
                CharacterInfo lover = Envir.GetCharacterInfo(Info.Married);
                PlayerObject ply = Envir.GetPlayer(lover.Name);
                if (ply != null) ply.GetRelationship(false);
            }

            CheckConquest(true);
        }
        public bool TeleportEscape(int attempts)
        {
            Map temp = Envir.GetMap(BindMapIndex);

            for (int i = 0; i < attempts; i++)
            {
                Point location = new Point(BindLocation.X + Envir.Random.Next(-100, 100),
                                           BindLocation.Y + Envir.Random.Next(-100, 100));

                if (Teleport(temp, location)) return true;
            }

            return false;
        }
        public override bool MagicTeleport(MagicInfoRunItem magic)
        {
            Map temp = Envir.GetMap(BindMapIndex);
            int mapSizeX = temp.Width / (magic.Level + 1);
            int mapSizeY = temp.Height / (magic.Level + 1);

            for (int i = 0; i < 200; i++)
            {
                Point location = new Point(BindLocation.X + Envir.Random.Next(-mapSizeX, mapSizeX),
                                     BindLocation.Y + Envir.Random.Next(-mapSizeY, mapSizeY));

                if (Teleport(temp, location)) return true;
            }

            return false;
        }

        public override bool IsAttackTarget(HumanObject attacker)
        {
            if (attacker == null || attacker.Node == null) return false;
            if (attacker.Race == ObjectType.Hero) attacker = ((HeroObject)attacker).Owner;
            if (Dead || InSafeZone || attacker.InSafeZone || attacker == this || GMGameMaster) return false;
            if (CurrentMap.Info.NoFight) return false;

            switch (attacker.AMode)
            {
                case AttackMode.All:
                    return true;
                case AttackMode.Group:
                    return GroupMembers == null || !GroupMembers.Contains(attacker);
                case AttackMode.Guild:
                    return MyGuild == null || MyGuild != attacker.MyGuild;
                case AttackMode.EnemyGuild:
                    return MyGuild != null && MyGuild.IsEnemy(attacker.MyGuild);
                case AttackMode.Peace:
                    return false;
                case AttackMode.RedBrown:
                    return PKPoints >= 200 || Envir.Time < BrownTime;
            }

            return true;
        }
        public override bool IsAttackTarget(MonsterObject attacker)
        {
            if (attacker == null || attacker.Node == null) return false;
            if (Dead || attacker.Master == this || GMGameMaster) return false;
            if (attacker.Info.AI == 6 || attacker.Info.AI == 58 || attacker.Info.AI == 113) return PKPoints >= 200;
            if (attacker.Master == null) return true;
            if (InSafeZone || attacker.InSafeZone || attacker.Master.InSafeZone) return false;

            if (LastHitter != attacker.Master && attacker.Master.LastHitter != this)
            {
                bool target = false;

                for (int i = 0; i < attacker.Master.Pets.Count; i++)
                {
                    if (attacker.Master.Pets[i].Target != this) continue;

                    target = true;
                    break;
                }

                if (!target)
                    return false;
            }

            switch (attacker.Master.AMode)
            {
                case AttackMode.All:
                    return true;
                case AttackMode.Group:
                    return GroupMembers == null || !GroupMembers.Contains(attacker.Master);
                case AttackMode.Guild:
                    return true;
                case AttackMode.EnemyGuild:
                    return false;
                case AttackMode.Peace:
                    return false;
                case AttackMode.RedBrown:
                    return PKPoints >= 200 || Envir.Time < BrownTime;
            }

            return true;
        }
        public override bool IsFriendlyTarget(HumanObject ally)
        {
            if (ally == this) return true;
            if (ally == Hero) return true;

            switch (ally.AMode)
            {
                case AttackMode.Group:
                    return GroupMembers != null && GroupMembers.Contains(ally);
                case AttackMode.RedBrown:
                    return PKPoints < 200 & Envir.Time > BrownTime;
                case AttackMode.Guild:
                    return MyGuild != null && MyGuild == ally.MyGuild;
                case AttackMode.EnemyGuild:
                    return MyGuild != null && !MyGuild.IsEnemy(ally.MyGuild);
                case AttackMode.All:
                    return false;
            }
            return true;
        }
        public override bool IsFriendlyTarget(MonsterObject ally)
        {
            if (ally.Race != ObjectType.Monster) return false;
            if (ally.Master == null) return false;

            switch (ally.Master.Race)
            {
                case ObjectType.Player:
                    if (!ally.Master.IsFriendlyTarget(this)) return false;
                    break;
                case ObjectType.Monster:
                    return false;
            }

            return true;
        }
        protected override void UpdateLooks(short OldLooks_Weapon)
        {
            base.UpdateLooks(OldLooks_Weapon);

            if (Globals.FishingRodShapes.Contains(OldLooks_Weapon) != Globals.FishingRodShapes.Contains(Looks_Weapon))
            {
                Enqueue(GetFishInfo());
            }
        }
        public override Packet GetInfo()
        {
            //should never use this but i leave it in for safety
            if (Observer) return null;

            string gName = "";
            string conquest = "";
            if (MyGuild != null)
            {
                gName = MyGuild.Name;
                if (MyGuild.Conquest != null)
                {
                    conquest = "[" + MyGuild.Conquest.Info.Name + "]";
                    gName = gName + conquest;
                }

            }

            return new S.ObjectPlayer
            {
                ObjectID = ObjectID,
                Name = CurrentMap.Info.NoNames ? "?????" : Name,
                NameColour = NameColour,
                GuildName = CurrentMap.Info.NoNames ? "?????" : gName,
                GuildRankName = CurrentMap.Info.NoNames ? "?????" : MyGuildRank != null ? MyGuildRank.Name : "",
                Class = Class,
                Gender = Gender,
                Level = Level,
                Location = CurrentLocation,
                Direction = Direction,
                Hair = Hair,
                Weapon = Looks_Weapon,
                WeaponEffect = Looks_WeaponEffect,
                Armour = Looks_Armour,
                Light = Light,
                Poison = CurrentPoison,
                Dead = Dead,
                Hidden = Hidden,
                Effect = HasBuff(BuffType.MagicShield, out _) ? SpellEffect.MagicShieldUp : HasBuff(BuffType.ElementalBarrier, out _) ? SpellEffect.ElementalBarrierUp : SpellEffect.None,
                WingEffect = Looks_Wings,
                MountType = Mount.MountType,
                RidingMount = RidingMount,
                Fishing = Fishing,

                TransformType = TransformType,

                ElementOrbEffect = (uint)GetElementalOrbCount(),
                ElementOrbLvl = (uint)ElementsLevel,
                ElementOrbMax = (uint)Settings.OrbsExpList[Settings.OrbsExpList.Count - 1],

                Buffs = Buffs.Where(d => d.Info.Visible).Select(e => e.Type).ToList(),

                LevelEffects = LevelEffects,

                // 原始传奇 origin 分层渲染：下发各层页游 id（客户端查 cm.item[col17] 或 cm.title[id].model 得模型号）。
                // 坐骑(MountType)/翅膀(Looks_Wings) 由服务端既有字段提供；装扮类由 CharacterInfo 的 Active* 字段提供（装扮系统激活后填充，默认 0 = 不绘制）。
                OriginArmourID = Info.Equipment[(int)EquipmentSlot.Armour]?.Info.Index ?? 0,
                OriginWeaponID = Info.Equipment[(int)EquipmentSlot.Weapon]?.Info.Index ?? 0,
                OriginWingID = Looks_Wings,
                OriginMountID = Mount != null ? Mount.MountType : 0,
                OriginZhuangBanID = Info.ActiveZhuangBan,  // 装扮（时装身体）
                OriginHuanWuID = Info.ActiveHuanWu,        // 幻武（时装武器）
                OriginFootID = Info.ActiveFoot,            // 足迹
                OriginTitleID = Info.ActiveTitle,          // 称号
                OriginHuiJiID = Info.ActiveHuiJi,          // 徽记
                OriginZhenfaID = Info.ActiveZhenFa,        // 法阵
                OriginXuanGuangID = Info.ActiveXuanGuang,  // 旋光
                // 页游装饰装备层（cm.item[type] 角色精灵；type：斗笠50/勋章51/玉佩52/护盾53/护肩54/披风55/面纱406/疗伤石）。默认 0 = 不绘制。
                // 新增 5 个装备槽优先驱动角色外观；Active* 保留为旧装扮系统的兜底。
                OriginDouLiID = EquippedIndex(EquipmentSlot.DouLi, Info.ActiveDouLi),
                OriginMianShaID = EquippedIndex(EquipmentSlot.MianSha, Info.ActiveMianSha),
                OriginYuPeiID = EquippedIndex(EquipmentSlot.YuPei, Info.ActiveYuPei),
                OriginXunZhangID = EquippedIndex(EquipmentSlot.XunZhang, Info.ActiveXunZhang),
                OriginHuDunID = Info.ActiveHuDun,
                OriginHuJianID = Info.ActiveHuJian,
                OriginPiFengID = Info.ActivePiFeng,
                OriginLiaoShangID = EquippedIndex(EquipmentSlot.LiaoShang, Info.ActiveLiaoShang),
            };
        }
        /// <summary>取指定装备槽里物品的 cm.item 键（物品 Index）；空槽回落到旧的装扮字段。</summary>
        private int EquippedIndex(EquipmentSlot slot, int fallback)
        {
            int i = (int)slot;
            UserItem item = Info.Equipment != null && i < Info.Equipment.Length ? Info.Equipment[i] : null;
            return item != null ? item.Info.Index : fallback;
        }

        public void EquipSlotItem(MirGridType grid, ulong id, int to, MirGridType gridTo, ulong idTo)
        {
            S.EquipSlotItem p = new S.EquipSlotItem { Grid = grid, UniqueID = id, To = to, GridTo = gridTo, Success = false };

            UserItem item = null;

            switch (gridTo)
            {
                case MirGridType.Mount:
                    item = Info.Equipment[(int)EquipmentSlot.Mount];
                    break;
                case MirGridType.Fishing:
                    item = Info.Equipment[(int)EquipmentSlot.Weapon];
                    break;
                case MirGridType.Socket:
                    UserItem temp2;
                    for (int i = 0; i < Info.Equipment.Length; i++)
                    {
                        temp2 = Info.Equipment[i];
                        if (temp2 == null || temp2.UniqueID != idTo) continue;
                        item = temp2;
                        break;
                    }
                    for (int i = 0; i < Info.Inventory.Length; i++)
                    {
                        temp2 = Info.Inventory[i];
                        if (temp2 == null || temp2.UniqueID != idTo) continue;
                        item = temp2;
                        break;
                    }
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            if (item == null || item.Slots == null)
            {
                Enqueue(p);
                return;
            }

            if (gridTo == MirGridType.Fishing && !item.Info.IsFishingRod)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= item.Slots.Length)
            {
                Enqueue(p);
                return;
            }

            if (item.Slots[to] != null)
            {
                Enqueue(p);
                return;
            }

            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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

                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }

                    if (Info.Equipment[to] != null &&
                        Info.Equipment[to].Info.Bind.HasFlag(BindMode.DontStore))
                    {
                        Enqueue(p);
                        return;
                    }
                    array = Account.Storage;
                    break;
                default:
                    Enqueue(p);
                    return;
            }


            int index = -1;
            UserItem temp = null;

            for (int i = 0; i < array.Length; i++)
            {
                temp = array[i];
                if (temp == null || temp.UniqueID != id) continue;
                index = i;
                break;
            }

            if (temp == null || index == -1)
            {
                Enqueue(p);
                return;
            }

            if ((item.Info.IsFishingRod || item.Info.Type == ItemType.Mount) && temp.Info.Type == ItemType.Socket)
            {
                Enqueue(p);
                return;
            }

            if (gridTo == MirGridType.Socket && temp.Info.Type != ItemType.Socket)
            {
                Enqueue(p);
                return;
            }

            if ((temp.SoulBoundId != -1) && (temp.SoulBoundId != Info.Index))
            {
                Enqueue(p);
                return;
            }

            if (CanUseItem(temp))
            {
                if (temp.Info.NeedIdentify && !temp.Identified)
                {
                    temp.Identified = true;
                    Enqueue(new S.RefreshItem { Item = temp });
                }

                switch (temp.Info.Shape)
                {
                    case 1:
                        if (item.Info.Type != ItemType.Weapon)
                        {
                            Enqueue(p);
                            return;
                        }
                        break;
                    case 2:
                        if (item.Info.Type != ItemType.Armour)
                        {
                            Enqueue(p);
                            return;
                        }
                        break;
                    case 3:
                        if (item.Info.Type != ItemType.Ring && item.Info.Type != ItemType.Bracelet && item.Info.Type != ItemType.Necklace)
                        {
                            Enqueue(p);
                            return;
                        }
                        break;
                }

                //if ((temp.Info.BindOnEquip) && (temp.SoulBoundId == -1))
                //{
                //    temp.SoulBoundId = Info.Index;
                //    Enqueue(new S.RefreshItem { Item = temp });
                //}
                //if (UnlockCurse && Info.Equipment[to].Cursed)
                //    UnlockCurse = false;

                item.Slots[to] = temp;
                array[index] = null;

                p.Success = true;
                Enqueue(p);
                RefreshStats();

                Report.ItemMoved(temp, grid, gridTo, index, to);

                return;
            }

            Enqueue(p);
        }
        public void RemoveItem(MirGridType grid, ulong id, int to)
        {
            S.RemoveItem p = new S.RemoveItem { Grid = grid, UniqueID = id, To = to, Success = false };
            UserItem[] toArray, fromArray;
            MirGridType fromGrid;
            switch (grid)
            {
                case MirGridType.Inventory:
                    toArray = Info.Inventory;
                    fromArray = Info.Equipment;
                    fromGrid = MirGridType.Equipment;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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

                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }

                    if (!Account.IsValidStorageIndex(to))
                    {
                        Enqueue(p);
                        return;
                    }

                    toArray = Account.Storage;
                    fromArray = Info.Equipment;
                    fromGrid = MirGridType.Equipment;
                    break;
                case MirGridType.HeroInventory:
                    if (!HasHero || !HeroSpawned)
                    {
                        Enqueue(p);
                        return;
                    }
                    toArray = CurrentHero.Inventory;
                    fromArray = CurrentHero.Equipment;
                    fromGrid = MirGridType.HeroEquipment;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            if (to < 0 || to >= toArray.Length) return;

            UserItem temp = null;
            int index = -1;

            for (int i = 0; i < fromArray.Length; i++)
            {
                temp = fromArray[i];
                if (temp == null || temp.UniqueID != id) continue;
                index = i;
                break;
            }

            if (temp == null || index == -1)
            {
                Enqueue(p);
                return;
            }

            if (temp.Cursed && !UnlockCurse)
            {
                Enqueue(p);
                return;
            }

            if (temp.WeddingRing != -1)
            {
                Enqueue(p);
                return;
            }

            if (temp.Info.Bind.HasFlag(BindMode.DontStore) && grid == MirGridType.Storage)
            {
                Enqueue(p);
                return;
            }

            if (!CanRemoveItem(grid, temp)) return;

            if (temp.Cursed)
                UnlockCurse = false;

            if (toArray[to] == null)
            {
                fromArray[index] = null;

                toArray[to] = temp;
                p.Success = true;
                Enqueue(p);
                if (grid == MirGridType.HeroInventory)
                {
                    Hero.RefreshStats();
                    Hero.Broadcast(GetUpdateInfo());
                }
                else
                {
                    RefreshStats();
                    Broadcast(GetUpdateInfo());
                }

                Report.ItemMoved(temp, fromGrid, grid, index, to);

                return;
            }

            Enqueue(p);
        }
        public void RemoveSlotItem(MirGridType grid, ulong id, int to, MirGridType gridTo, ulong idFrom)
        {
            S.RemoveSlotItem p = new S.RemoveSlotItem { Grid = grid, UniqueID = id, To = to, GridTo = gridTo, Success = false };
            UserItem[] array;
            switch (gridTo)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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

                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }

                    if (!Account.IsValidStorageIndex(to))
                    {
                        Enqueue(p);
                        return;
                    }

                    array = Account.Storage;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            if (to < 0 || to >= array.Length) return;

            UserItem temp = null;
            UserItem slotTemp = null;
            int index = -1;

            switch (grid)
            {
                case MirGridType.Mount:
                    temp = Info.Equipment[(int)EquipmentSlot.Mount];
                    break;
                case MirGridType.Fishing:
                    temp = Info.Equipment[(int)EquipmentSlot.Weapon];
                    break;
                case MirGridType.Socket:
                    UserItem temp2;
                    for (int i = 0; i < Info.Equipment.Length; i++)
                    {
                        temp2 = Info.Equipment[i];
                        if (temp2 == null || temp2.UniqueID != idFrom) continue;
                        temp = temp2;
                        break;
                    }
                    for (int i = 0; i < Info.Inventory.Length; i++)
                    {
                        temp2 = Info.Inventory[i];
                        if (temp2 == null || temp2.UniqueID != idFrom) continue;
                        temp = temp2;
                        break;
                    }
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            if (temp == null || temp.Slots == null)
            {
                Enqueue(p);
                return;
            }

            if (grid == MirGridType.Fishing && !temp.Info.IsFishingRod)
            {
                Enqueue(p);
                return;
            }

            for (int i = 0; i < temp.Slots.Length; i++)
            {
                slotTemp = temp.Slots[i];
                if (slotTemp == null || slotTemp.UniqueID != id) continue;
                index = i;
                break;
            }

            if (slotTemp == null || index == -1)
            {
                Enqueue(p);
                return;
            }

            if (slotTemp.Cursed && !UnlockCurse)
            {
                Enqueue(p);
                return;
            }

            if (slotTemp.WeddingRing != -1)
            {
                Enqueue(p);
                return;
            }

            if (!CanRemoveItem(gridTo, slotTemp)) return;

            temp.Slots[index] = null;

            if (slotTemp.Cursed)
                UnlockCurse = false;

            if (array[to] == null)
            {
                array[to] = slotTemp;
                p.Success = true;
                Enqueue(p);
                RefreshStats();
                Broadcast(GetUpdateInfo());

                Report.ItemMoved(temp, grid, gridTo, index, to);

                return;
            }

            Enqueue(p);
        }
        public void MoveItem(MirGridType grid, int from, int to)
        {
            S.MoveItem p = new S.MoveItem { Grid = grid, From = from, To = to, Success = false };
            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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

                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }

                    if (!Account.IsValidStorageIndex(to) || !Account.IsValidStorageIndex(from))
                    {
                        Enqueue(p);
                        return;
                    }

                    array = Account.Storage;
                    break;
                case MirGridType.Trade:
                    array = Info.Trade;
                    TradeItem();
                    break;
                case MirGridType.Refine:
                    array = Info.Refine;
                    break;
                case MirGridType.HeroInventory:
                    if (!HasHero || !HeroSpawned)
                    {
                        Enqueue(p);
                        return;
                    }
                    array = CurrentHero.Inventory;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            if (from >= 0 && to >= 0 && from < array.Length && to < array.Length)
            {
                if (array[from] == null)
                {
                    Report.ItemError(grid, grid, from, to);
                    ReceiveChat(LanguageMgr.Get("ItemMoveErrorReport"), ChatType.System);
                    Enqueue(p);
                    return;
                }

                UserItem i = array[to];
                array[to] = array[from];

                Report.ItemMoved(array[to], grid, grid, from, to);

                array[from] = i;

                Report.ItemMoved(array[from], grid, grid, to, from);

                p.Success = true;
                Enqueue(p);
                return;
            }

            Enqueue(p);
        }

        /// <summary>背包整理（传奇标准）：
        /// 1) 前 BeltSize(5) 个物品快捷栏完全不参与；
        /// 2) 同类可堆叠物品先合并成满堆，腾出格子（照 AddItem/MergeItem 的同类堆叠规则）；
        /// 3) 按一定规则排序：物品大类 → 品质(高在前) → 需求等级(高在前) → Index → 数量；
        /// 4) 从 BeltSize 起依次放进【前面的空格子】，空槽全部留在末尾（紧凑，前排不留空洞）。
        /// 合并会删物品、改数量，逐格 S.MoveItem 交换表达不了，故整理完整体下发背包数组 S.RefreshInventory。</summary>
        public void SortInventory()
        {
            UserItem[] array = Info.Inventory;
            int start = BeltSize; // 跳过前 5 个物品快捷栏（腰带槽）

            // 收集背包区（快捷栏之后）的所有非空物品
            var items = new List<UserItem>();
            for (int i = start; i < array.Length; i++)
                if (array[i] != null) items.Add(array[i]);

            if (items.Count == 0) return;

            // 1) 同类可堆叠合并：同一 ItemInfoTable、未满堆，把后面的数量并进前面的堆，腾出格子
            for (int i = 0; i < items.Count; i++)
            {
                UserItem dst = items[i];
                if (dst == null || dst.Info == null || dst.Info.StackSize <= 1 || dst.Count >= dst.Info.StackSize) continue;

                for (int j = i + 1; j < items.Count && dst.Count < dst.Info.StackSize; j++)
                {
                    UserItem src = items[j];
                    // 按 Index 判定同类（GetRealItem 可能对同一配置生成不同 ItemInfoTable 实例）
                    if (src == null || src.Info == null || src.Info.Index != dst.Info.Index) continue;

                    int take = Math.Min((int)Math.Min(dst.Info.StackSize, ushort.MaxValue) - dst.Count, (int)src.Count);
                    if (take <= 0) continue;

                    dst.Count += (ushort)take;
                    src.Count -= (ushort)take;
                    if (src.Count == 0) items[j] = null; // 整堆被吸收
                }
            }
            items.RemoveAll(x => x == null || x.Count == 0);

            // 2) 排序：大类聚拢，同类按品质→需求等级→Index→数量
            items.Sort((a, b) =>
            {
                ItemInfoTable ia = a.Info, ib = b.Info;
                if (ia == null || ib == null) return 0;

                int c = ((int)ia.Type).CompareTo((int)ib.Type);          // 物品大类
                if (c != 0) return c;
                c = ((int)ib.Grade).CompareTo((int)ia.Grade);             // 品质高在前
                if (c != 0) return c;
                c = ib.RequiredAmount.CompareTo(ia.RequiredAmount);       // 需求等级高在前
                if (c != 0) return c;
                c = ia.Index.CompareTo(ib.Index);                         // 同类按 Index
                if (c != 0) return c;
                return b.Count.CompareTo(a.Count);                        // 数量多在前
            });

            // 3) 清空背包区，依次放入前面的空格子；空槽全部留在末尾
            for (int i = start; i < array.Length; i++) array[i] = null;
            int slot = start;
            foreach (UserItem item in items) array[slot++] = item;

            RefreshBagWeight();
            RefreshStats();

            // 4) 整体下发整理后的背包（含空槽），客户端替换数组并刷新界面
            Enqueue(new S.RefreshInventory { Inventory = (UserItem[])array.Clone() });
        }
        public void StoreItem(int from, int to)
        {
            S.StoreItem p = new S.StoreItem { From = from, To = to, Success = false };

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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

            if (!CanAccessStorage())
            {
                Enqueue(p);
                return;
            }

            if (from < 0 || from >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Account.Storage.Length)
            {
                Enqueue(p);
                return;
            }

            if (!Account.IsValidStorageIndex(to))
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

            if (temp.Info.Bind.HasFlag(BindMode.DontStore))
            {
                Enqueue(p);
                return;
            }

            if (temp.RentalInformation != null && temp.RentalInformation.BindingFlags.HasFlag(BindMode.DontStore))
            {
                Enqueue(p);
                return;
            }

            if (Account.Storage[to] == null)
            {
                Account.Storage[to] = temp;
                Info.Inventory[from] = null;
                RefreshBagWeight();

                Report.ItemMoved(temp, MirGridType.Inventory, MirGridType.Storage, from, to);

                p.Success = true;
                Enqueue(p);
                return;
            }
            Enqueue(p);
        }
        public void TakeBackItem(int from, int to)
        {
            S.TakeBackItem p = new S.TakeBackItem { From = from, To = to, Success = false };

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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

            if (!CanAccessStorage())
            {
                Enqueue(p);
                return;
            }

            if (from < 0 || from >= Account.Storage.Length)
            {
                Enqueue(p);
                return;
            }

            if (!Account.IsValidStorageIndex(from))
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem temp = Account.Storage[from];

            if (temp == null)
            {
                Enqueue(p);
                return;
            }

            if (Info.Inventory[to] == null)
            {
                Info.Inventory[to] = temp;
                Account.Storage[from] = null;

                Report.ItemMoved(temp, MirGridType.Storage, MirGridType.Inventory, from, to);

                p.Success = true;
                RefreshBagWeight();
                Enqueue(p);

                return;
            }
            Enqueue(p);
        }
        public void EquipItem(MirGridType grid, ulong id, int to)
        {
            S.EquipItem p = new S.EquipItem { Grid = grid, UniqueID = id, To = to, Success = false };

            if ((grid == MirGridType.Inventory || grid == MirGridType.Storage) && Fishing)
            {
                Enqueue(p);
                return;
            }

            UserItem[] toArray = null;
            MirGridType toGrid = MirGridType.Equipment;
            HumanObject actor = this;
            switch (grid)
            {
                case MirGridType.Inventory:
                case MirGridType.Storage:
                    toArray = Info.Equipment;
                    break;
                case MirGridType.HeroInventory:
                    if (HasHero && HeroSpawned && !Hero.Dead)
                    {
                        toArray = CurrentHero.Equipment;
                        toGrid = MirGridType.HeroEquipment;
                        actor = Hero;
                    }
                    break;
            }

            if (toArray == null || to < 0 || to >= toArray.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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
                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }
                    array = Account.Storage;
                    break;
                case MirGridType.HeroInventory:
                    if (!HasHero || !HeroSpawned)
                    {
                        Enqueue(p);
                        return;
                    }
                    array = CurrentHero.Inventory;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            int index = -1;
            UserItem temp = null;

            for (int i = 0; i < array.Length; i++)
            {
                temp = array[i];
                if (temp == null || temp.UniqueID != id) continue;
                index = i;
                break;
            }

            if (temp == null || index == -1)
            {
                Enqueue(p);
                return;
            }

            if ((toArray[to] != null) && (toArray[to].Cursed) && (!UnlockCurse))
            {
                Enqueue(p);
                return;
            }

            if (grid == MirGridType.Storage)
            {
                if (!Account.IsValidStorageIndex(index))
                {
                    Enqueue(p);
                    return;
                }
            }

            if ((temp.SoulBoundId != -1) && (temp.SoulBoundId != Info.Index))
            {
                Enqueue(p);
                return;
            }

            if (toArray[to] != null)
                if (toArray[to].WeddingRing != -1)
                {
                    Enqueue(p);
                    return;
                }
            if (toArray[to] != null &&
                toArray[to].Info.Bind.HasFlag(BindMode.DontStore))
            {
                Enqueue(p);
                return;
            }

            if (actor.CanEquipItem(temp, to))
            {
                if (temp.Info.NeedIdentify && !temp.Identified)
                {
                    temp.Identified = true;
                    Enqueue(new S.RefreshItem { Item = temp });
                }
                if ((temp.Info.Bind.HasFlag(BindMode.BindOnEquip)) && (temp.SoulBoundId == -1))
                {
                    temp.SoulBoundId = Info.Index;
                    Enqueue(new S.RefreshItem { Item = temp });
                }

                if ((toArray[to] != null) && (toArray[to].Cursed) && (UnlockCurse))
                    UnlockCurse = false;

                array[index] = toArray[to];

                Report.ItemMoved(temp, toGrid, grid, to, index, "RemoveItem");

                toArray[to] = temp;

                Report.ItemMoved(temp, grid, toGrid, index, to);

                p.Success = true;
                Enqueue(p);
                if (toGrid == MirGridType.HeroEquipment)
                    Hero.RefreshStats();
                else
                    RefreshStats();

                //Broadcast(GetUpdateInfo());
                return;
            }
            Enqueue(p);
        }
        public void TakeBackHeroItem(int from, int to)
        {
            S.TakeBackHeroItem p = new S.TakeBackHeroItem { From = from, To = to, Success = false };

            if (!HasHero || !HeroSpawned || Hero.Dead)
            {
                Enqueue(p);
                return;
            }

            if (from < 0 || from >= CurrentHero.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem temp = CurrentHero.Inventory[from];

            if (temp == null)
            {
                Enqueue(p);
                return;
            }

            if (Info.Inventory[to] == null)
            {
                Info.Inventory[to] = temp;
                CurrentHero.Inventory[from] = null;

                Report.ItemMoved(temp, MirGridType.HeroInventory, MirGridType.Inventory, from, to);

                p.Success = true;
                RefreshBagWeight();
                Hero.RefreshBagWeight();
                Enqueue(p);

                return;
            }
            Enqueue(p);
        }
        public void TransferHeroItem(int from, int to)
        {
            S.TransferHeroItem p = new S.TransferHeroItem { From = from, To = to, Success = false };

            if (!HasHero || !HeroSpawned || Hero.Dead)
            {
                Enqueue(p);
                return;
            }

            if (from < 0 || from >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= CurrentHero.Inventory.Length)
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

            if (temp.Info.Bind.HasFlag(BindMode.NoHero))
            {
                Enqueue(p);
                return;
            }

            if (temp.Weight + Hero.CurrentBagWeight > Hero.Stats[Stat.BagWeight])
            {
                ReceiveChat(LanguageMgr.Get("TooHeavyToTransfer"), ChatType.System);
                Enqueue(p);
                return;
            }

            if (CurrentHero.Inventory[to] == null)
            {
                CurrentHero.Inventory[to] = temp;
                Info.Inventory[from] = null;

                Report.ItemMoved(temp, MirGridType.Inventory, MirGridType.HeroInventory, from, to);

                p.Success = true;
                RefreshBagWeight();
                Hero.RefreshBagWeight();
                Enqueue(p);

                return;
            }
            Enqueue(p);
        }
        public override void UseItem(ulong id)
        {
            S.UseItem p = new S.UseItem { UniqueID = id, Grid = MirGridType.Inventory, Success = false };

            UserItem item = null;
            int index = -1;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                item = Info.Inventory[i];
                if (item == null || item.UniqueID != id) continue;
                index = i;
                break;
            }

            if (item == null || index == -1 || !CanUseItem(item))
            {
                Enqueue(p);
                return;
            }

            if (Dead && !(item.Info.Type == ItemType.Scroll && item.Info.Shape == 6))
            {
                Enqueue(p);
                return;
            }

            // 原始传奇技能书：SkillBookInfoTable 映射（物品 id → 技能/目标等级），转换器产出 Type=0 不走 ItemType.Book。
            SkillBookInfoTable book = SkillBookInfoTable.Find(item.Info.Index);
            if (book != null)
            {
                MagicInfoTable learned = null;
                for (int i = 0; i < Envir.MagicInfoList.Count; i++)
                {
                    MagicInfoTable info = Envir.MagicInfoList[i];
                    if (info != null && info.OriginId == book.SkillId) { learned = info; break; }
                }

                if (learned == null)
                {
                    Enqueue(p);
                    return;
                }

                if (Level < book.NeedLevel)
                {
                    ReceiveOutputMessage($"使用技能书 <color=red>等级不足</color>", OutputMessageType.Normal);
                    Enqueue(p);
                    return;
                }

                MagicInfoRunItem existing = GetMagic(learned.Spell);
                if (existing == null)
                {
                    existing = new MagicInfoRunItem(learned.Spell);
                    if (existing.Info == null) { Enqueue(p); return; }
                    Info.Magics.Add(existing);
                }

                // 原版 TargetLevel=1 是 1 级书（激活）；4/5/6/7 是升级书。
                // MaxLevel=书决定的等级上限（1~3），Level=当前实际等级（0 起，靠熟练度提升）。
                byte targetLevel = book.TargetLevel <= 1 ? (byte)1 : (byte)Math.Min(book.TargetLevel - 2, 3);

                if (book.TargetLevel <= 1)
                {
                    if (existing.IsLearned())
                    {
                        ReceiveChat("已经学会该技能", ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    existing.MaxLevel = targetLevel;
                    existing.Level = 0;
                    existing.Experience = 0;
                    // 原始传奇 JS: openNewSkill → ncm.err("恭喜您已领悟：【"+技能名+"】")
                    // 走左上角提示区（OutputMessageType.Quest），不塞进聊天框。
                    ReceiveOutputMessage($"<color=lime>恭喜</color> <color=lime>学会</color>【{learned.Name}】", OutputMessageType.Normal);
                }
                else
                {
                    if (!existing.IsLearned())
                    {
                        ReceiveChat("请先用低级技能书激活技能", ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    if (existing.MaxLevel >= targetLevel)
                    {
                        ReceiveChat("技能等级已达标", ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    // 升级书要求熟练度攒满当前实际等级（Need1/2），对齐原版 skilled + cost 双条件。
                    ushort need = existing.Level == 0 ? learned.Need1 : learned.Need2;
                    if (existing.Experience < need)
                    {
                        ReceiveChat("技能熟练度不足", ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    existing.MaxLevel = targetLevel;
                    existing.Experience = 0;
                    ReceiveOutputMessage($"<color=lime>恭喜</color> <color=lime>学会</color>【{learned.Name}】", OutputMessageType.Normal);
                }

                SendMagicInfo(existing);
                Enqueue(new S.MagicDelay { ObjectID = ObjectID, Spell = existing.Spell, Delay = existing.GetDelay() });
                RefreshStats();

                CheckSkillGoalQuests(); // 原始传奇"学习技能"支线：学会对应技能即自动完成领奖

                if (item.Count > 1) item.Count--;
                else Info.Inventory[index] = null;
                RefreshBagWeight();
                Report.ItemChanged(item, 1, 1);

                p.Success = true;
                Enqueue(p);
                return;
            }

            // 直接读原始传奇 1config.item：col33=1（获得时自动使用）+ col22="物品id#数量"，右键使用即展开。
            if (Envir.TryGetOriginAutoUseRewards(item.Info.Index, out List<(int ItemIndex, int Count)> originRewards))
            {
                List<UserItem> rewardItems = new List<UserItem>();
                foreach ((int rewardId, int rewardCount) in originRewards)
                {
                    ItemInfoTable rewardInfo = Envir.GetItemInfo(rewardId);
                    if (rewardInfo == null)
                    {
                        throw new InvalidOperationException($"[使用物品] 礼盒 {item.Info.Index} 的奖励物品 {rewardId} 不在物品表中");
                    }

                    UserItem rewardItem = Envir.CreateFreshItem(rewardInfo);
                    rewardItem.Count = (ushort)Math.Min(item.Count * rewardCount, ushort.MaxValue);
                    rewardItems.Add(rewardItem);
                }
                foreach (UserItem rewardItem in rewardItems)
                {
                    if (!CanGainItem(rewardItem))
                    {
                        ReceiveChat("背包空间不足，无法开启礼盒。", ChatType.System);
                        Enqueue(p);
                        return;
                    }
                }

                foreach (UserItem rewardItem in rewardItems)
                {
                    AddItem(rewardItem);
                }

                if (item.Count > 1) item.Count--;
                else Info.Inventory[index] = null;
                RefreshBagWeight();
                Report.ItemChanged(item, 1, 1);

                p.Success = true;
                Enqueue(p);
                return;
            }

            switch (item.Info.Type)
            {
                case ItemType.Potion:
                    switch (item.Info.Shape)
                    {
                        case 0: //NormalPotion
                            PotHealthAmount = (ushort)Math.Min(ushort.MaxValue, PotHealthAmount + item.Info.Stats[Stat.HP]);
                            PotManaAmount = (ushort)Math.Min(ushort.MaxValue, PotManaAmount + item.Info.Stats[Stat.MP]);
                            break;
                        case 1: //SunPotion
                            ChangeHP(item.Info.Stats[Stat.HP]);
                            ChangeMP(item.Info.Stats[Stat.MP]);
                            break;
                        case 2: //MysteryWater
                            if (UnlockCurse)
                            {
                                ReceiveChat(LanguageMgr.Get("CanAlreadyUnequipCursedItem"), ChatType.System);
                                Enqueue(p);
                                return;
                            }
                            ReceiveChat(LanguageMgr.Get("CanNowUnequipCursedItem"), ChatType.System);
                            UnlockCurse = true;
                            break;
                        case 3: //Buff
                            {
                                int time = item.Info.Durability;

                                if (item.GetTotal(Stat.MaxDC) > 0 || item.GetTotal(Stat.MinDC) > 0)
                                    AddBuff(BuffType.Impact, this, time * Settings.Minute, new Stats { [Stat.MaxDC] = item.GetTotal(Stat.MaxDC), [Stat.MinDC] = item.GetTotal(Stat.MinDC) });

                                if (item.GetTotal(Stat.MaxMC) > 0 || item.GetTotal(Stat.MinMC) > 0)
                                    AddBuff(BuffType.Magic, this, time * Settings.Minute, new Stats { [Stat.MaxMC] = item.GetTotal(Stat.MaxMC), [Stat.MinMC] = item.GetTotal(Stat.MinMC) });

                                if (item.GetTotal(Stat.MaxSC) > 0 || item.GetTotal(Stat.MinSC) > 0)
                                    AddBuff(BuffType.Taoist, this, time * Settings.Minute, new Stats { [Stat.MaxSC] = item.GetTotal(Stat.MaxSC), [Stat.MinSC] = item.GetTotal(Stat.MinSC) });

                                if (item.GetTotal(Stat.AttackSpeed) > 0)
                                    AddBuff(BuffType.Storm, this, time * Settings.Minute, new Stats { [Stat.AttackSpeed] = item.GetTotal(Stat.AttackSpeed) });

                                if (item.GetTotal(Stat.HP) > 0)
                                    AddBuff(BuffType.HealthAid, this, time * Settings.Minute, new Stats { [Stat.HP] = item.GetTotal(Stat.HP) });

                                if (item.GetTotal(Stat.MP) > 0)
                                    AddBuff(BuffType.ManaAid, this, time * Settings.Minute, new Stats { [Stat.MP] = item.GetTotal(Stat.MP) });

                                if (item.GetTotal(Stat.MaxAC) > 0 || item.GetTotal(Stat.MinAC) > 0)
                                    AddBuff(BuffType.Defence, this, time * Settings.Minute, new Stats { [Stat.MaxAC] = item.GetTotal(Stat.MaxAC), [Stat.MinAC] = item.GetTotal(Stat.MinAC) });

                                if (item.GetTotal(Stat.MaxMAC) > 0 || item.GetTotal(Stat.MinMAC) > 0)
                                    AddBuff(BuffType.MagicDefence, this, time * Settings.Minute, new Stats { [Stat.MaxMAC] = item.GetTotal(Stat.MaxMAC), [Stat.MinMAC] = item.GetTotal(Stat.MinMAC) });

                                if (item.GetTotal(Stat.BagWeight) > 0)
                                    AddBuff(BuffType.BagWeight, this, time * Settings.Minute, new Stats { [Stat.BagWeight] = item.GetTotal(Stat.BagWeight) });
                            }
                            break;
                        case 4: //Exp
                            {
                                int time = item.Info.Durability;
                                AddBuff(BuffType.Exp, this, Settings.Minute * time, new Stats { [Stat.ExpRatePercent] = item.GetTotal(Stat.Luck) });
                            }
                            break;
                        case 5: //Drop
                            {
                                int time = item.Info.Durability;
                                AddBuff(BuffType.Drop, this, Settings.Minute * time, new Stats { [Stat.ItemDropRatePercent] = item.GetTotal(Stat.Luck) });
                            }
                            break;
                    }
                    break;
                case ItemType.Scroll:
                    UserItem temp;
                    switch (item.Info.Shape)
                    {
                        case 0: //DE
                            if (!TeleportEscape(20))
                            {
                                Enqueue(p);
                                return;
                            }
                            foreach (DelayedAction ac in ActionList.Where(u => u.Type == DelayedType.NPC))
                            {
                                ac.FlaggedToRemove = true;
                            }
                            break;
                        case 1: //TT
                            if (!Teleport(Envir.GetMap(BindMapIndex), BindLocation))
                            {
                                Enqueue(p);
                                return;
                            }
                            foreach (DelayedAction ac in ActionList.Where(u => u.Type == DelayedType.NPC))
                            {
                                ac.FlaggedToRemove = true;
                            }
                            break;
                        case 2: //RT
                            if (!TeleportRandom(200, item.Info.Durability))
                            {
                                Enqueue(p);
                                return;
                            }
                            foreach (DelayedAction ac in ActionList.Where(u => u.Type == DelayedType.NPC))
                            {
                                ac.FlaggedToRemove = true;
                            }
                            break;
                        case 3: //BenedictionOil
                            if (!TryLuckWeapon())
                            {
                                Enqueue(p);
                                return;
                            }
                            break;
                        case 4: //RepairOil
                            temp = Info.Equipment[(int)EquipmentSlot.Weapon];
                            if (temp == null || temp.MaxDura == temp.CurrentDura)
                            {
                                Enqueue(p);
                                return;
                            }
                            if (temp.Info.Bind.HasFlag(BindMode.DontRepair))
                            {
                                Enqueue(p);
                                return;
                            }
                            temp.MaxDura = (ushort)Math.Max(0, temp.MaxDura - Math.Min(5000, temp.MaxDura - temp.CurrentDura) / 30);

                            temp.CurrentDura = (ushort)Math.Min(temp.MaxDura, temp.CurrentDura + 5000);
                            temp.DuraChanged = false;

                            ReceiveChat(LanguageMgr.Get("WeaponPartiallyRepaired"), ChatType.System);
                            Enqueue(new S.ItemRepaired { UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });
                            break;
                        case 5: //WarGodOil
                            temp = Info.Equipment[(int)EquipmentSlot.Weapon];
                            if (temp == null || temp.MaxDura == temp.CurrentDura)
                            {
                                Enqueue(p);
                                return;
                            }
                            if (temp.Info.Bind.HasFlag(BindMode.DontRepair) || (temp.Info.Bind.HasFlag(BindMode.NoSRepair)))
                            {
                                Enqueue(p);
                                return;
                            }
                            temp.CurrentDura = temp.MaxDura;
                            temp.DuraChanged = false;

                            ReceiveChat(LanguageMgr.Get("WeaponCompletelyRepaired"), ChatType.System);
                            Enqueue(new S.ItemRepaired { UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });
                            break;
                        case 6: //ResurrectionScroll
                            if (CurrentMap.Info.NoReincarnation)
                            {
                                ReceiveChat(LanguageMgr.Get("CannotUseOnMap"), ChatType.System);
                                Enqueue(p);
                                return;
                            }
                            if (Dead)
                            {
                                MP = Stats[Stat.MP];
                                Revive(MaxHealth, true);
                            }
                            break;
                        case 7: //CreditScroll
                            if (item.Info.Price > 0)
                            {
                                GainCredit(item.Info.Price);
                                ReceiveChat(LanguageMgr.Get("CreditsAddedToAccount", item.Info.Price), ChatType.System);
                            }
                            break;
                        case 8: //MapShoutScroll
                            HasMapShout = true;
                            ReceiveChat(LanguageMgr.Get("FreeMapShout"), ChatType.System);
                            break;
                        case 9://ServerShoutScroll
                            HasServerShout = true;
                            ReceiveChat(LanguageMgr.Get("FreeServerShout"), ChatType.System);
                            break;
                        case 10://GuildSkillScroll
                            MyGuild.NewBuff(item.Info.Effect, false);
                            break;
                        case 11://HomeTeleport
                            if (MyGuild != null && MyGuild.Conquest != null && !MyGuild.Conquest.WarIsOn && MyGuild.Conquest.PalaceMap != null && !TeleportRandom(200, 0, MyGuild.Conquest.PalaceMap))
                            {
                                Enqueue(p);
                                return;
                            }
                            break;
                        case 12://LotteryTicket                                                                                    
                            if (Envir.Random.Next(item.Info.Effect * 32) == 1) // 1st prize : 1,000,000
                            {
                                ReceiveChat(LanguageMgr.Get("FirstPrizeGoldReward"), ChatType.System);
                                GainGold(1000000);
                            }
                            else if (Envir.Random.Next(item.Info.Effect * 16) == 1)  // 2nd prize : 200,000
                            {
                                ReceiveChat(LanguageMgr.Get("WonSecondPrizeGold"), ChatType.System);
                                GainGold(200000);
                            }
                            else if (Envir.Random.Next(item.Info.Effect * 8) == 1)  // 3rd prize : 100,000
                            {
                                ReceiveChat(LanguageMgr.Get("WonThirdPrizeGold"), ChatType.System);
                                GainGold(100000);
                            }
                            else if (Envir.Random.Next(item.Info.Effect * 4) == 1) // 4th prize : 10,000
                            {
                                ReceiveChat(LanguageMgr.Get("WonFourthPrizeGold"), ChatType.System);
                                GainGold(10000);
                            }
                            else if (Envir.Random.Next(item.Info.Effect * 2) == 1)  // 5th prize : 1,000
                            {
                                ReceiveChat(LanguageMgr.Get("WonFifthPrizeGold"), ChatType.System);
                                GainGold(1000);
                            }
                            else if (Envir.Random.Next(item.Info.Effect) == 1)  // 6th prize 500
                            {
                                ReceiveChat(LanguageMgr.Get("WonSixthPrizeGold"), ChatType.System);
                                GainGold(500);
                            }
                            else
                            {
                                ReceiveChat(LanguageMgr.Get("WonNothing"), ChatType.System);
                            }
                            break;
                        case 13://Hero unlock autopot
                            if (!HeroSpawned || Hero.AutoPot)
                            {
                                Enqueue(p);
                                return;
                            }
                            Hero.AutoPot = true;
                            Enqueue(new S.UnlockHeroAutoPot());
                            ReceiveChat(LanguageMgr.Get("HeroAutoPotUnlocked"), ChatType.System);
                            break;
                        case 14: //Increase maximum hero count
                            if (Info.MaximumHeroCount >= Settings.MaximumHeroCount)
                            {
                                ReceiveChat(LanguageMgr.Get("MaximumHeroCountReached"), ChatType.System);
                                Enqueue(p);
                                return;
                            }
                            Info.MaximumHeroCount++;
                            Array.Resize(ref Info.Heroes, Info.MaximumHeroCount);
                            break;
                        case 15: //Increase Hero Inventory
                            ReceiveChat(LanguageMgr.Get("MustBeUsedOnHero"), ChatType.System);
                            Enqueue(p);
                            break;
                    }
                    break;
                case ItemType.Book:
                    MagicInfoRunItem magic = new MagicInfoRunItem((Spell)item.Info.Shape);

                    if (magic.Info == null)
                    {
                        Enqueue(p);
                        return;
                    }

                    Info.Magics.Add(magic);
                    SendMagicInfo(magic);
                    RefreshStats();
                    break;
                case ItemType.Script:
                    CallDefaultNPC(DefaultNPCType.UseItem, item.Info.Shape);
                    break;
                case ItemType.Food:
                    temp = Info.Equipment[(int)EquipmentSlot.Mount];
                    if (temp == null || temp.MaxDura == temp.CurrentDura)
                    {
                        Enqueue(p);
                        return;
                    }

                    switch (item.Info.Shape)
                    {
                        case 0:
                            temp.MaxDura = (ushort)Math.Max(0, temp.MaxDura - Math.Min(1000, temp.MaxDura - (temp.CurrentDura / 30)));
                            break;
                        case 1:
                            break;
                    }

                    temp.CurrentDura = (ushort)Math.Min(temp.MaxDura, temp.CurrentDura + item.CurrentDura);
                    temp.DuraChanged = false;

                    ReceiveChat(LanguageMgr.Get("MountFed"), ChatType.System);
                    Enqueue(new S.ItemRepaired { UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });

                    RefreshStats();
                    break;
                case ItemType.Pets:
                    if (item.Info.Shape >= 20)
                    {
                        switch (item.Info.Shape)
                        {
                            case 20://Mirror
                                {
                                    Enqueue(new S.IntelligentCreatureEnableRename());
                                }
                                break;
                            case 21://BlackStone
                                {
                                    if (item.Count > 1) item.Count--;
                                    else Info.Inventory[index] = null;
                                    RefreshBagWeight();
                                    p.Success = true;
                                    Enqueue(p);
                                    BlackstoneRewardItem();
                                }
                                return;
                            case 22://Nuts
                                {
                                    if (CreatureSummoned)
                                    {
                                        for (int i = 0; i < Pets.Count; i++)
                                        {
                                            if (Pets[i].Race != ObjectType.Creature) continue;

                                            var pet = (IntelligentCreatureObject)Pets[i];
                                            if (pet.PetType != SummonedCreatureType) continue;
                                            pet.MaintainfoodTime = item.Info.Effect * Settings.Hour / 1000;
                                            break;
                                        }
                                    }
                                }
                                break;
                            case 23://FairyMoss, FreshwaterClam, Mackerel, Cherry
                                {
                                    if (CreatureSummoned)
                                    {
                                        for (int i = 0; i < Pets.Count; i++)
                                        {
                                            if (Pets[i].Race != ObjectType.Creature) continue;

                                            var pet = (IntelligentCreatureObject)Pets[i];
                                            if (pet.PetType != SummonedCreatureType) continue;
                                            if (pet.Fullness < 10000)
                                            {
                                                pet.IncreaseFullness(item.Info.Effect * 100);
                                            }
                                            break;
                                        }
                                    }
                                }
                                break;
                            case 24://WonderPill
                                {
                                    if (CreatureSummoned)
                                    {
                                        for (int i = 0; i < Pets.Count; i++)
                                        {
                                            if (Pets[i].Race != ObjectType.Creature) continue;

                                            var pet = (IntelligentCreatureObject)Pets[i];
                                            if (pet.PetType != SummonedCreatureType) continue;
                                            if (pet.Fullness == 0)
                                            {
                                                pet.IncreaseFullness(100);
                                            }
                                            break;
                                        }
                                    }
                                }
                                break;
                            case 25://Strongbox
                                {
                                    byte boxtype = item.Info.Effect;
                                    if (item.Count > 1) item.Count--;
                                    else Info.Inventory[index] = null;
                                    RefreshBagWeight();
                                    p.Success = true;
                                    Enqueue(p);
                                    StrongboxRewardItem(boxtype);
                                }
                                break;
                            case 26://Wonderdrug
                                {
                                    if (HasBuff(BuffType.WonderDrug, out _))
                                    {
                                        ReceiveChat(LanguageMgr.Get("WonderDrugActive"), ChatType.System);
                                        Enqueue(p);
                                        return;
                                    }

                                    var time = item.Info.Durability;

                                    AddBuff(BuffType.WonderDrug, this, time * Settings.Minute, new Stats(item.AddedStats));
                                }
                                break;
                            case 27://FortuneCookies
                                break;
                            case 28://Knapsack
                                {
                                    var time = item.Info.Durability;

                                    AddBuff(BuffType.Knapsack, this, time * Settings.Minute, new Stats { [Stat.BagWeight] = item.GetTotal(Stat.Luck) });
                                }
                                break;
                        }
                    }
                    else
                    {
                        int slotIndex = Info.IntelligentCreatures.Count;
                        UserIntelligentCreature petInfo = new UserIntelligentCreature((IntelligentCreatureType)item.Info.Shape, slotIndex, item.Info.Effect);
                        if (Info.CheckHasIntelligentCreature((IntelligentCreatureType)item.Info.Shape))
                        {
                            ReceiveChat(LanguageMgr.Get("AlreadyHaveCreature"), ChatType.System);
                            petInfo = null;
                        }

                        if (petInfo == null || slotIndex >= 10)
                        {
                            Enqueue(p);
                            return;
                        }

                        ReceiveChat(LanguageMgr.Get("ObtainedNewCreature", petInfo.CustomName), ChatType.System);

                        Info.IntelligentCreatures.Add(petInfo);
                        Enqueue(petInfo.GetInfo());
                    }
                    break;
                case ItemType.Transform: //Transforms
                    {
                        AddBuff(BuffType.Transform, this, (Settings.Second * item.Info.Durability), new Stats(), values: item.Info.Shape);
                    }
                    break;
                case ItemType.Deco:

                    DecoObject decoOb = new DecoObject
                    {
                        Image = item.Info.Shape,
                        CurrentMap = CurrentMap,
                        CurrentLocation = CurrentLocation,
                    };

                    CurrentMap.AddObject(decoOb);
                    decoOb.Spawned();

                    Enqueue(decoOb.GetInfo());

                    break;
                case ItemType.MonsterSpawn:

                    var monsterID = item.Info.Stats[Stat.HP];
                    var spawnAsPet = item.Info.Shape == 1;
                    var conquestOnly = item.Info.Shape == 2;

                    var monsterInfo = Envir.GetMonsterInfo(monsterID);
                    if (monsterInfo == null) break;

                    MonsterObject monster = MonsterObject.GetMonster(monsterInfo);
                    if (monster == null) break;

                    if (spawnAsPet)
                    {
                        if (Pets.Count(t => !t.Dead && t.Race != ObjectType.Creature) >= Globals.MaxPets)
                        {
                            ReceiveChat(LanguageMgr.Get("MaximumPetsReached"), ChatType.System);
                            Enqueue(p);
                            return;
                        }

                        monster.Master = this;
                        monster.PetLevel = 0;
                        monster.MaxPetLevel = 7;

                        Pets.Add(monster);
                    }

                    if (conquestOnly)
                    {
                        var con = CurrentMap.GetConquest(CurrentLocation);
                        if (con == null)
                        {
                            ReceiveChat(LanguageMgr.Get("SpawnOnlyDuringConquest", monsterInfo.GameName), ChatType.System);
                            Enqueue(p);
                            return;
                        }
                    }

                    monster.Direction = Direction;
                    monster.ActionTime = Envir.Time + 5000;

                    if (!monster.Spawn(CurrentMap, Front))
                        monster.Spawn(CurrentMap, CurrentLocation);
                    break;
                case ItemType.SiegeAmmo:
                    //TODO;
                    break;
                case ItemType.SealedHero:
                    HeroInfo heroInfo = Envir.GetHeroInfo(item.AddedStats[Stat.Hero]);
                    if (heroInfo == null || !AddHero(heroInfo))
                    {
                        Enqueue(p);
                        return;
                    }
                    break;
                default:
                    return;
            }

            if (item.Count > 1) item.Count--;
            else Info.Inventory[index] = null;
            RefreshBagWeight();

            Report.ItemChanged(item, 1, 1);

            p.Success = true;
            Enqueue(p);
        }
        public void HeroUseItem(ulong id)
        {
            if (!HasHero || !HeroSpawned)
                return;
            Hero.UseItem(id);
        }
        public void SplitItem(MirGridType grid, ulong id, ushort count)
        {
            S.SplitItem1 p = new S.SplitItem1 { Grid = grid, UniqueID = id, Count = count, Success = false };
            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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
                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }
                    array = Account.Storage;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            UserItem temp = null;


            var index = -1;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == null || array[i].UniqueID != id) continue;
                index = i;
                temp = array[i];
                break;
            }

            if (temp == null || index == -1 || count >= temp.Count || FreeSpace(array) == 0 || count < 1)
            {
                Enqueue(p);
                return;
            }

            if (grid == MirGridType.Storage)
            {
                var nindex = -1;
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i] != null) continue;
                    nindex = i;
                    break;
                }

                if (!Account.IsValidStorageIndex(index) || !Account.IsValidStorageIndex(nindex))
                {
                    Enqueue(p);
                    return;
                }
            }

            temp.Count -= count;

            var originalItem = temp;

            temp = Envir.CreateFreshItem(temp.Info);
            temp.Count = count;

            Report.ItemSplit(originalItem, temp, grid);

            p.Success = true;
            Enqueue(p);
            Enqueue(new S.SplitItem { Item = temp, Grid = grid });

            if (grid == MirGridType.Inventory && (temp.Info.Type == ItemType.Potion || temp.Info.Type == ItemType.Scroll || temp.Info.Type == ItemType.Amulet || (temp.Info.Type == ItemType.Script && temp.Info.Effect == 1)))
            {
                if (temp.Info.Type == ItemType.Potion || temp.Info.Type == ItemType.Scroll || (temp.Info.Type == ItemType.Script && temp.Info.Effect == 1))
                {
                    for (int i = PotionBeltMinimum; i < PotionBeltMaximum; i++)
                    {
                        if (array[i] != null) continue;
                        array[i] = temp;
                        RefreshBagWeight();
                        return;
                    }
                }
                else if (temp.Info.Type == ItemType.Amulet)
                {
                    for (int i = AmuletBeltMinimum; i < AmuletBeltMaximum; i++)
                    {
                        if (array[i] != null) continue;
                        array[i] = temp;
                        RefreshBagWeight();
                        return;
                    }
                }
            }

            for (int i = BeltSize; i < array.Length; i++)
            {
                if (array[i] != null) continue;
                array[i] = temp;
                RefreshBagWeight();
                return;
            }

            for (int i = 0; i < BeltSize; i++)
            {
                if (array[i] != null) continue;
                array[i] = temp;
                RefreshBagWeight();
                return;
            }
        }
        public void MergeItem(MirGridType gridFrom, MirGridType gridTo, ulong fromID, ulong toID)
        {
            S.MergeItem p = new S.MergeItem { GridFrom = gridFrom, GridTo = gridTo, IDFrom = fromID, IDTo = toID, Success = false };

            UserItem[] arrayFrom;

            switch (gridFrom)
            {
                case MirGridType.Inventory:
                    arrayFrom = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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
                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayFrom = Account.Storage;
                    break;
                case MirGridType.Equipment:
                    arrayFrom = Info.Equipment;
                    break;
                case MirGridType.Fishing:
                    if (Info.Equipment[(int)EquipmentSlot.Weapon] == null || !Info.Equipment[(int)EquipmentSlot.Weapon].Info.IsFishingRod)
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayFrom = Info.Equipment[(int)EquipmentSlot.Weapon].Slots;
                    break;
                case MirGridType.HeroInventory:
                    if (!HasHero || !HeroSpawned)
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayFrom = CurrentHero.Inventory;
                    break;
                case MirGridType.HeroEquipment:
                    if (!HasHero || !HeroSpawned)
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayFrom = CurrentHero.Equipment;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            UserItem[] arrayTo;
            switch (gridTo)
            {
                case MirGridType.Inventory:
                    arrayTo = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.StorageKey, StringComparison.CurrentCultureIgnoreCase))
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
                    if (!CanAccessStorage())
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayTo = Account.Storage;
                    break;
                case MirGridType.Equipment:
                    arrayTo = Info.Equipment;
                    break;
                case MirGridType.Fishing:
                    if (Info.Equipment[(int)EquipmentSlot.Weapon] == null || !Info.Equipment[(int)EquipmentSlot.Weapon].Info.IsFishingRod)
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayTo = Info.Equipment[(int)EquipmentSlot.Weapon].Slots;
                    break;
                case MirGridType.HeroInventory:
                    if (!HasHero || !HeroSpawned)
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayTo = CurrentHero.Inventory;
                    break;
                case MirGridType.HeroEquipment:
                    if (!HasHero || !HeroSpawned)
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayTo = CurrentHero.Equipment;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            UserItem tempFrom = null;
            int index = -1;

            for (int i = 0; i < arrayFrom.Length; i++)
            {
                if (arrayFrom[i] == null || arrayFrom[i].UniqueID != fromID) continue;
                index = i;
                tempFrom = arrayFrom[i];
                break;
            }

            if (tempFrom == null || tempFrom.Info.StackSize == 1 || index == -1)
            {
                Enqueue(p);
                return;
            }

            if (gridFrom == MirGridType.Storage)
            {
                if (!Account.IsValidStorageIndex(index))
                {
                    Enqueue(p);
                    return;
                }
            }


            UserItem tempTo = null;
            int toIndex = -1;

            for (int i = 0; i < arrayTo.Length; i++)
            {
                if (arrayTo[i] == null || arrayTo[i].UniqueID != toID) continue;
                toIndex = i;
                tempTo = arrayTo[i];
                break;
            }

            if (tempTo == null || tempTo.Info != tempFrom.Info || tempTo.Count == tempTo.Info.StackSize)
            {
                Enqueue(p);
                return;
            }

            if (gridTo == MirGridType.Storage)
            {
                if (!Account.IsValidStorageIndex(toIndex))
                {
                    Enqueue(p);
                    return;
                }
            }

            if (tempTo.Info.Type != ItemType.Amulet && (gridFrom == MirGridType.Equipment || gridTo == MirGridType.Equipment))
            {
                Enqueue(p);
                return;
            }

            if (tempTo.Info.Type != ItemType.Bait && (gridFrom == MirGridType.Fishing || gridTo == MirGridType.Fishing))
            {
                Enqueue(p);
                return;
            }

            if (tempFrom.Count <= tempTo.Info.StackSize - tempTo.Count)
            {
                tempTo.Count += tempFrom.Count;
                arrayFrom[index] = null;
            }
            else
            {
                tempFrom.Count -= (ushort)(tempTo.Info.StackSize - tempTo.Count);
                tempTo.Count = (ushort)Math.Min(tempTo.Info.StackSize, ushort.MaxValue);
            }

            Report.ItemMerged(tempFrom, tempTo, index, toIndex, gridFrom, gridTo);

            TradeUnlock();

            p.Success = true;
            Enqueue(p);
            RefreshStats();
        }
        public void CombineItem(MirGridType grid, ulong fromID, ulong toID)
        {
            S.CombineItem p = new S.CombineItem { Grid = grid, IDFrom = fromID, IDTo = toID, Success = false };

            UserItem[] array = null;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.HeroInventory:
                    if (HasHero && HeroSpawned)
                        array = CurrentHero.Inventory;
                    break;
            }

            if (array == null)
            {
                Enqueue(p);
                return;
            }

            UserItem tempFrom = null;
            UserItem tempTo = null;
            int indexFrom = -1;
            int indexTo = -1;

            if (Dead)
            {
                Enqueue(p);
                return;
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == null || array[i].UniqueID != fromID) continue;
                indexFrom = i;
                tempFrom = array[i];
                break;
            }

            if (tempFrom == null || indexFrom == -1)
            {
                Enqueue(p);
                return;
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == null || array[i].UniqueID != toID) continue;
                indexTo = i;
                tempTo = array[i];
                break;
            }

            if (tempTo == null || indexTo == -1)
            {
                Enqueue(p);
                return;
            }

            if ((byte)tempTo.Info.Type < 1 || (byte)tempTo.Info.Type > 11)
            {
                Enqueue(p);
                return;
            }

            bool canRepair = false, canUpgrade = false, canSlotUpgrade = false, canSeal = false;

            if (tempFrom.Info.Type != ItemType.Gem)
            {
                Enqueue(p);
                return;
            }

            switch (tempFrom.Info.Shape)
            {
                case 1: //BoneHammer
                case 2: //SewingSupplies
                case 5: //SpecialHammer
                case 6: //SpecialSewingSupplies

                    if (tempTo.Info.Bind.HasFlag(BindMode.DontRepair))
                    {
                        Enqueue(p);
                        return;
                    }

                    switch (tempTo.Info.Type)
                    {
                        case ItemType.Weapon:
                        case ItemType.Necklace:
                        case ItemType.Ring:
                        case ItemType.Bracelet:
                            if (tempFrom.Info.Shape == 1 || tempFrom.Info.Shape == 5)
                                canRepair = true;
                            break;
                        case ItemType.Armour:
                        case ItemType.Helmet:
                        case ItemType.Boots:
                        case ItemType.Belt:
                            if (tempFrom.Info.Shape == 2 || tempFrom.Info.Shape == 6)
                                canRepair = true;
                            break;
                        default:
                            canRepair = false;
                            break;
                    }

                    if (canRepair != true)
                    {
                        Enqueue(p);
                        return;
                    }

                    if (tempTo.CurrentDura == tempTo.MaxDura)
                    {
                        ReceiveChat(LanguageMgr.Get("ItemNoRepairNeeded"), ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    break;
                case 7: //slots
                    if (tempTo.Info.Bind.HasFlag(BindMode.DontUpgrade) || tempTo.Info.Unique != SpecialItemMode.None)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (tempTo.RentalInformation != null && tempTo.RentalInformation.BindingFlags.HasFlag(BindMode.DontUpgrade))
                    {
                        Enqueue(p);
                        return;
                    }
                    if (!ValidGemForItem(tempFrom, (byte)tempTo.Info.Type))
                    {
                        ReceiveChat(LanguageMgr.Get("InvalidCombination"), ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    if (tempTo.Info.RandomStats == null)
                    {
                        ReceiveChat(LanguageMgr.Get("ItemMaxSockets"), ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    if (tempTo.Info.RandomStats.SlotMaxStat <= tempTo.Slots.Length)
                    {
                        ReceiveChat(LanguageMgr.Get("ItemMaxSockets"), ChatType.System);
                        Enqueue(p);
                        return;
                    }

                    canSlotUpgrade = true;
                    break;
                case 8: //Seal
                    if (tempTo.Info.Bind.HasFlag(BindMode.DontUpgrade) || tempTo.Info.Unique != SpecialItemMode.None)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (tempTo.SealedInfo != null && tempTo.SealedInfo.ExpiryDate > Envir.Now)
                    {
                        ReceiveChat(LanguageMgr.Get("ItemAlreadySealed"), ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    if (tempTo.SealedInfo != null && tempTo.SealedInfo.NextSealDate > Envir.Now)
                    {
                        double remainingSeconds = (tempTo.SealedInfo.NextSealDate - Envir.Now).TotalSeconds;

                        ReceiveChat(LanguageMgr.Get("ItemCannotBeResealedFor", Functions.PrintTimeSpanFromSeconds(remainingSeconds, false)), ChatType.System);
                        Enqueue(p);
                        return;
                    }

                    canSeal = true;
                    break;
                case 3: //gems
                case 4: //orbs
                    if (tempTo.Info.Bind.HasFlag(BindMode.DontUpgrade) || tempTo.Info.Unique != SpecialItemMode.None)
                    {
                        Enqueue(p);
                        return;
                    }

                    if (tempTo.RentalInformation != null && tempTo.RentalInformation.BindingFlags.HasFlag(BindMode.DontUpgrade))
                    {
                        Enqueue(p);
                        return;
                    }

                    if ((tempTo.GemCount >= tempFrom.Info.Stats[Stat.CriticalDamage]) || (GetCurrentStatCount(tempFrom, tempTo) >= tempFrom.Info.Stats[Stat.HPDrainRatePercent]))
                    {
                        ReceiveChat(LanguageMgr.Get("ItemMaxAddedStats"), ChatType.System);
                        Enqueue(p);
                        return;
                    }

                    int successchance = tempFrom.Info.Stats[Stat.Reflect];

                    // Gem is only affected by the stat applied.
                    // Drop rate per gem won't work if gems add more than 1 stat, i.e. DC + 2 per gem.
                    if (Settings.GemStatIndependent)
                    {
                        Stat GemType = GetGemType(tempFrom);

                        switch (GemType)
                        {
                            case Stat.MaxAC:
                                successchance *= (int)tempTo.AddedStats[Stat.MaxAC];
                                break;

                            case Stat.MaxMAC:
                                successchance *= (int)tempTo.AddedStats[Stat.MaxMAC];
                                break;

                            case Stat.MaxDC:
                                successchance *= (int)tempTo.AddedStats[Stat.MaxDC];
                                break;

                            case Stat.MaxMC:
                                successchance *= (int)tempTo.AddedStats[Stat.MaxMC];
                                break;

                            case Stat.MaxSC:
                                successchance *= (int)tempTo.AddedStats[Stat.MaxSC];
                                break;

                            case Stat.AttackSpeed:
                                successchance *= (int)tempTo.AddedStats[Stat.AttackSpeed];
                                break;

                            case Stat.Accuracy:
                                successchance *= (int)tempTo.AddedStats[Stat.Accuracy];
                                break;

                            case Stat.Agility:
                                successchance *= (int)tempTo.AddedStats[Stat.Agility];
                                break;

                            case Stat.Freezing:
                                successchance *= (int)tempTo.AddedStats[Stat.Freezing];
                                break;

                            case Stat.PoisonAttack:
                                successchance *= (int)tempTo.AddedStats[Stat.PoisonAttack];
                                break;

                            case Stat.MagicResist:
                                successchance *= (int)tempTo.AddedStats[Stat.MagicResist];
                                break;

                            case Stat.PoisonResist:
                                successchance *= (int)tempTo.AddedStats[Stat.PoisonResist];
                                break;

                            // These attributes may not work as more than 1 stat is
                            // added per gem, i.e + 40 HP.

                            case Stat.HP:
                                successchance *= (int)tempTo.AddedStats[Stat.HP];
                                break;

                            case Stat.MP:
                                successchance *= (int)tempTo.AddedStats[Stat.MP];
                                break;

                            case Stat.HealthRecovery:
                                successchance *= (int)tempTo.AddedStats[Stat.HealthRecovery];
                                break;

                            // I don't know if this conflicts with benes.
                            case Stat.Luck:
                                successchance *= (int)tempTo.AddedStats[Stat.Luck];
                                break;

                            case Stat.Strong:
                                successchance *= (int)tempTo.AddedStats[Stat.Strong];
                                break;

                            case Stat.PoisonRecovery:
                                successchance *= (int)tempTo.AddedStats[Stat.PoisonRecovery];
                                break;


                            /*
                                 Currently not supported.
                                 Missing item definitions.

                                 case StatType.HP_Precent:
                                 case StatType.MP_Precent:
                                 case StatType.MP_Regen:
                                 case StatType.Holy:
                                 case StatType.Durability:


                            */
                            default:
                                successchance *= (int)tempTo.GemCount;
                                break;

                        }
                    }
                    // Gem is affected by the total added stats on the item.
                    else
                    {
                        successchance *= (int)tempTo.GemCount;
                    }

                    successchance = successchance >= tempFrom.Info.Stats[Stat.CriticalRate] ? 0 : (tempFrom.Info.Stats[Stat.CriticalRate] - successchance) + Stats[Stat.GemRatePercent];

                    //check if combine will succeed
                    bool succeeded = Envir.Random.Next(100) < successchance;
                    canUpgrade = true;

                    byte itemType = (byte)tempTo.Info.Type;

                    if (!ValidGemForItem(tempFrom, itemType))
                    {
                        ReceiveChat(LanguageMgr.Get("InvalidCombination"), ChatType.System);
                        Enqueue(p);
                        return;
                    }

                    if (tempFrom.GetTotal(Stat.MaxDC) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.MaxDC] += tempFrom.GetTotal(Stat.MaxDC);
                    }

                    else if (tempFrom.GetTotal(Stat.MaxMC) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.MaxMC] += tempFrom.GetTotal(Stat.MaxMC);
                    }

                    else if (tempFrom.GetTotal(Stat.MaxSC) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.MaxSC] += tempFrom.GetTotal(Stat.MaxSC);
                    }

                    else if (tempFrom.GetTotal(Stat.MaxAC) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.MaxAC] += tempFrom.GetTotal(Stat.MaxAC);
                    }

                    else if (tempFrom.GetTotal(Stat.MaxMAC) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.MaxMAC] += tempFrom.GetTotal(Stat.MaxMAC);
                    }

                    else if ((tempFrom.Info.Durability) > 0)
                    {
                        if (succeeded) tempTo.MaxDura = (ushort)Math.Min(ushort.MaxValue, tempTo.MaxDura + tempFrom.MaxDura);
                    }

                    else if (tempFrom.GetTotal(Stat.AttackSpeed) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.AttackSpeed] += tempFrom.GetTotal(Stat.AttackSpeed);
                    }

                    else if (tempFrom.GetTotal(Stat.Agility) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.Agility] += tempFrom.GetTotal(Stat.Agility);
                    }

                    else if (tempFrom.GetTotal(Stat.Accuracy) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.Accuracy] += tempFrom.GetTotal(Stat.Accuracy);
                    }

                    else if (tempFrom.GetTotal(Stat.PoisonAttack) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.PoisonAttack] += tempFrom.GetTotal(Stat.PoisonAttack);
                    }

                    else if (tempFrom.GetTotal(Stat.Freezing) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.Freezing] += tempFrom.GetTotal(Stat.Freezing);
                    }

                    else if (tempFrom.GetTotal(Stat.MagicResist) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.MagicResist] += tempFrom.GetTotal(Stat.MagicResist);
                    }

                    else if (tempFrom.GetTotal(Stat.PoisonResist) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.PoisonResist] += tempFrom.GetTotal(Stat.PoisonResist);
                    }
                    else if (tempFrom.GetTotal(Stat.Luck) > 0)
                    {
                        if (succeeded) tempTo.AddedStats[Stat.Luck] += tempFrom.GetTotal(Stat.Luck);
                    }
                    else
                    {
                        ReceiveChat(LanguageMgr.Get("CannotCombineItems"), ChatType.System);
                        Enqueue(p);
                        return;
                    }

                    if (!succeeded)
                    {
                        if ((tempFrom.Info.Shape == 3) && (Envir.Random.Next(15) < 3))
                        {
                            //item destroyed
                            ReceiveChat(LanguageMgr.Get("ItemHasBeenDestroyed"), ChatType.System);
                            Report.ItemChanged(array[indexTo], 1, 1, "CombineItem (Item Destroyed)");

                            array[indexTo] = null;
                            p.Destroy = true;
                        }
                        else
                        {
                            //upgrade has no effect
                            ReceiveChat(LanguageMgr.Get("UpgradeNoEffect"), ChatType.System);
                        }

                        canUpgrade = false;
                    }
                    break;
                default:
                    Enqueue(p);
                    return;
            }


            switch (grid)
            {
                case MirGridType.Inventory:
                    RefreshBagWeight();
                    break;
                case MirGridType.HeroInventory:
                    Hero.RefreshBagWeight();
                    break;
            }

            if (canRepair && array[indexTo] != null)
            {
                switch (tempTo.Info.Shape)
                {
                    case 1:
                    case 2:
                        {
                            tempTo.MaxDura = (ushort)Math.Max(0, Math.Min(tempTo.MaxDura, tempTo.MaxDura - 100 * Envir.Random.Next(10)));
                        }
                        break;
                    default:
                        break;
                }
                tempTo.CurrentDura = tempTo.MaxDura;
                tempTo.DuraChanged = false;

                ReceiveChat(LanguageMgr.Get("ItemRepaired"), ChatType.System);
                Enqueue(new S.ItemRepaired { UniqueID = tempTo.UniqueID, MaxDura = tempTo.MaxDura, CurrentDura = tempTo.CurrentDura });
            }

            if (canUpgrade && array[indexTo] != null)
            {
                tempTo.GemCount++;
                ReceiveChat(LanguageMgr.Get("ItemUpgraded"), ChatType.System);
                Enqueue(new S.ItemUpgraded { Item = tempTo });
            }

            if (canSlotUpgrade && array[indexTo] != null)
            {
                tempTo.SetSlotSize(tempTo.Slots.Length + 1);
                ReceiveChat(LanguageMgr.Get("ItemSocketsIncreased"), ChatType.System);
                Enqueue(new S.ItemSlotSizeChanged { UniqueID = tempTo.UniqueID, SlotSize = tempTo.Slots.Length });
            }

            if (canSeal && array[indexTo] != null)
            {
                var minutes = tempFrom.CurrentDura;
                tempTo.SealedInfo = new SealedInfo
                {
                    ExpiryDate = Envir.Now.AddMinutes(minutes),
                    NextSealDate = Envir.Now.AddMinutes(minutes).AddMinutes(Settings.ItemSealDelay)
                };

                ReceiveChat(LanguageMgr.Get("ItemSealedFor", Functions.PrintTimeSpanFromSeconds(minutes * 60)), ChatType.System);

                Enqueue(new S.ItemSealChanged { UniqueID = tempTo.UniqueID, ExpiryDate = tempTo.SealedInfo.ExpiryDate });
            }

            if (tempFrom.Count > 1) tempFrom.Count--;
            else array[indexFrom] = null;

            Report.ItemCombined(tempFrom, tempTo, indexFrom, indexTo, grid);

            //item merged ok
            TradeUnlock();

            p.Success = true;
            Enqueue(p);
        }
        private bool ValidGemForItem(UserItem Gem, byte itemtype)
        {
            switch (itemtype)
            {
                case 1: //weapon
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Paralize))
                        return true;
                    break;
                case 2: //Armour
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Teleport))
                        return true;
                    break;
                case 4: //Helmet
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.ClearRing))
                        return true;
                    break;
                case 5: //necklace
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Protection))
                        return true;
                    break;
                case 6: //bracelet
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Revival))
                        return true;
                    break;
                case 7: //ring
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Muscle))
                        return true;
                    break;
                case 8: //amulet
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Flame))
                        return true;
                    break;
                case 9://belt
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Healing))
                        return true;
                    break;
                case 10: //boots
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Probe))
                        return true;
                    break;
                case 11: //stone
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.Skill))
                        return true;
                    break;
                case 12:///torch
                    if (Gem.Info.Unique.HasFlag(SpecialItemMode.NoDuraLoss))
                        return true;
                    break;
            }
            return false;
        }
        //Gems granting multiple stat types are not compatiable with this method.
        private Stat GetGemType(UserItem gem)
        {
            if (gem.GetTotal(Stat.MaxDC) > 0)
                return Stat.MaxDC;

            else if (gem.GetTotal(Stat.MaxMC) > 0)
                return Stat.MaxMC;

            else if (gem.GetTotal(Stat.MaxSC) > 0)
                return Stat.MaxSC;

            else if (gem.GetTotal(Stat.MaxAC) > 0)
                return Stat.MaxAC;

            else if (gem.GetTotal(Stat.MaxMAC) > 0)
                return Stat.MaxMAC;

            else if (gem.GetTotal(Stat.AttackSpeed) > 0)
                return Stat.AttackSpeed;

            else if (gem.GetTotal(Stat.Agility) > 0)
                return Stat.Agility;

            else if (gem.GetTotal(Stat.Accuracy) > 0)
                return Stat.Accuracy;

            else if (gem.GetTotal(Stat.PoisonAttack) > 0)
                return Stat.PoisonAttack;

            else if (gem.GetTotal(Stat.Freezing) > 0)
                return Stat.Freezing;

            else if (gem.GetTotal(Stat.MagicResist) > 0)
                return Stat.MagicResist;

            else if (gem.GetTotal(Stat.PoisonResist) > 0)
                return Stat.PoisonResist;

            else if (gem.GetTotal(Stat.Luck) > 0)
                return Stat.Luck;

            else if (gem.GetTotal(Stat.PoisonRecovery) > 0)
                return Stat.PoisonRecovery;

            else if (gem.GetTotal(Stat.HP) > 0)
                return Stat.HP;

            else if (gem.GetTotal(Stat.MP) > 0)
                return Stat.MP;

            else if (gem.GetTotal(Stat.HealthRecovery) > 0)
                return Stat.HealthRecovery;

            // These may be incomplete. Item definitions may be missing?

            else if (gem.GetTotal(Stat.HPRatePercent) > 0)
                return Stat.HPRatePercent;

            else if (gem.GetTotal(Stat.MPRatePercent) > 0)
                return Stat.MPRatePercent;

            else if (gem.GetTotal(Stat.SpellRecovery) > 0)
                return Stat.SpellRecovery;

            else if (gem.GetTotal(Stat.Holy) > 0)
                return Stat.Holy;

            else if (gem.GetTotal(Stat.Strong) > 0)
                return Stat.Strong;

            else if (gem.GetTotal(Stat.HPDrainRatePercent) > 0)
                return Stat.HPDrainRatePercent;

            return Stat.Unknown;
        }

        public void DeleteItem(ulong id, ushort count)
        {
            var resp = new S.DeleteItem { UniqueID = id, Count = count };

            if (Dead)
            {
                Enqueue(resp);
                return;
            }

            UserItem item = null;
            int idx = -1;

            // Only delete from PLAYER inventory (no Hero inventory here)
            var array = Info.Inventory;

            // Find by UniqueID
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == null) continue;
                if (array[i].UniqueID != id) continue;
                item = array[i];
                idx = i;
                break;
            }

            if (item == null)
            {
                Enqueue(resp);
                return;
            }

            if (count == 0 || count > item.Count) count = item.Count;

            // Adjust or remove
            if (count < item.Count)
                item.Count -= count;
            else
                array[idx] = null;

            Report?.ItemDeleted(item, count, "InventoryDelete");

            RefreshBagWeight();
            Enqueue(resp);
        }

        //Gems granting multiple stat types are not compatible with this method.        
        public void DropItem(ulong id, ushort count, bool isHeroItem)
        {
            S.DropItem p = new S.DropItem { UniqueID = id, Count = count, HeroItem = isHeroItem, Success = false };
            if (Dead)
            {
                Enqueue(p);
                return;
            }

            if (CurrentMap.Info.NoThrowItem)
            {
                ReceiveChat(LanguageMgr.Get("CanNotDrop"), ChatType.System);
                Enqueue(p);
                return;
            }

            UserItem temp = null;
            int index = -1;
            HeroObject currentHero = null;

            if (!isHeroItem)
            {
                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    temp = Info.Inventory[i];
                    if (temp == null || temp.UniqueID != id) continue;
                    index = i;
                    break;
                }
            }
            else
            {
                currentHero = Envir.Heroes.FirstOrDefault(h => h.Info.Index == Info.CurrentHeroIndex);

                if (currentHero != null)
                {
                    for (int i = 0; i < currentHero.Info.Inventory.Length; i++)
                    {
                        temp = currentHero.Info.Inventory[i];
                        if (temp == null || temp.UniqueID != id) continue;
                        index = i;
                        break;
                    }
                }
                else
                {
                    Enqueue(p);
                    return;
                }
            }

            if (temp == null || index == -1 || count > temp.Count || count < 1)
            {
                Enqueue(p);
                return;
            }

            if (temp.Info.Bind.HasFlag(BindMode.DontDrop))
            {
                Enqueue(p);
                return;
            }

            if (temp.RentalInformation != null && temp.RentalInformation.BindingFlags.HasFlag(BindMode.DontDrop))
            {
                Enqueue(p);
                return;
            }

            if (temp.Count == count)
            {
                if (!temp.Info.Bind.HasFlag(BindMode.DestroyOnDrop))
                    if (!DropItem(temp))
                    {
                        Enqueue(p);
                        return;
                    }

                if (p.HeroItem)
                {
                    currentHero.Info.Inventory[index] = null;
                }
                else
                {
                    Info.Inventory[index] = null;
                }

            }
            else
            {
                UserItem temp2 = Envir.CreateFreshItem(temp.Info);
                temp2.Count = count;
                if (!temp.Info.Bind.HasFlag(BindMode.DestroyOnDrop))
                    if (!DropItem(temp2))
                    {
                        Enqueue(p);
                        return;
                    }
                temp.Count -= count;
            }
            p.Success = true;
            Enqueue(p);

            if (p.HeroItem)
            {
                currentHero.RefreshBagWeight();
                currentHero.Report.ItemChangedHero(temp, count, 1);
            }
            else
            {
                RefreshBagWeight();
                Report.ItemChanged(temp, count, 1);
            }
        }
        public void DropGold(uint gold)
        {
            if (Info.Gold < gold) return;

            ItemObject ob = new ItemObject(this, gold);

            if (!ob.Drop(5)) return;
            Info.Gold -= gold;
            Enqueue(new S.LoseGold { Gold = gold });
        }
        public void PickUp()
        {
            if (Dead)
            {
                //Send Fail
                return;
            }

            Cell cell = CurrentMap.GetCell(CurrentLocation);

            bool sendFail = false;

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];

                if (ob.Race != ObjectType.Item) continue;

                if (ob.Owner != null && ob.Owner != this && !IsGroupMember(ob.Owner)) //Or Group member.
                {
                    sendFail = true;
                    continue;
                }
                ItemObject item = (ItemObject)ob;

                if (item.Item != null)
                {
                    // 先检查此物品是否为当前任务所需：是则收集进任务背包（并移除地面物品），
                    // 否则只进普通背包会导致任务收集进度（ProcessItem）不更新。原 PickUp 漏了这步。
                    if (CheckGroupQuestItem(item.Item))
                    {
                        CurrentMap.RemoveObject(ob);
                        ob.Despawn();
                        return;
                    }

                    if (!CanGainItem(item.Item)) continue;

                    if (item.Item.Info.ShowGroupPickup && IsGroupMember(this))
                        for (int j = 0; j < GroupMembers.Count; j++)
                            GroupMembers[j].ReceiveChat(LanguageMgr.Get("PickedUpItem", Name, item.Item.FriendlyName), ChatType.System);

                    GainItem(item.Item);

                    Report.ItemChanged(item.Item, item.Item.Count, 2);

                    CurrentMap.RemoveObject(ob);
                    ob.Despawn();

                    return;
                }

                if (!CanGainGold(item.Gold)) continue;

                GainGold(item.Gold);
                CurrentMap.RemoveObject(ob);
                ob.Despawn();
                return;
            }

            if (sendFail)
                ReceiveChat(LanguageMgr.Get("CannotPickupNotOwner"), ChatType.System);

        }
        public void RequestItemInfo(int itemIndex)
        {
            if (itemIndex <= 0 || Connection == null) return;

            var info = Envir.GetItemInfo(itemIndex);
            if (info == null) return;

            Connection.CheckItemInfo(info);
        }

        public void RequestMonsterInfo(int monsterIndex)
        {
            if (monsterIndex <= 0 || Connection == null) return;

            var info = Envir.GetMonsterInfo(monsterIndex);
            if (info == null) return;

            Connection.CheckMonsterInfo(info);
        }

        public void RequestNPCInfo(int npcIndex)
        {
            if (npcIndex <= 0 || Connection == null) return;

            var info = Envir.GetNPCInfo(npcIndex);
            if (info == null) return;

            Connection.CheckNPCInfo(info);
        }

        public void RequestMapInfo(int mapIndex)
        {
            var info = Envir.GetMapInfo(mapIndex);
            CheckMapInfo(info);
        }
        public void TeleportToNPC(uint objectID)
        {
            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                NPCObject ob = CurrentMap.NPCs[i];
                if (ob.ObjectID != objectID) continue;

                if (!ob.Info.CanTeleportTo) return;

                uint cost = (uint)Settings.TeleportToNPCCost;
                if (Info.Gold < cost) return;

                Point p = ob.Front;
                if (!CurrentMap.ValidPoint(p))
                {
                    for (int j = 0; j < 7; j++)
                    {
                        p = Functions.PointMove(CurrentLocation, Functions.ShiftDirection(ob.Direction, j), 1);
                        if (CurrentMap.ValidPoint(p)) break;
                    }
                }

                if (CurrentMap.ValidPoint(p))
                {
                    Info.Gold -= cost;
                    Enqueue(new S.LoseGold { Gold = cost });
                    Teleport(CurrentMap, p);
                }

                break;
            }
        }

        /// <summary>
        /// 任务追踪栏"点击传送"：免费跨图传送到任务目标（怪物刷点 / NPC）周边。
        /// 仅当请求的坐标匹配玩家当前任务里某个未完成任务的目标时才放行，防止被滥用为任意坐标免费传送。
        /// </summary>
        public void TeleportToQuestTarget(uint mapIndex, int x, int y, uint monsterIndex = 0)
        {
            MessageQueue.Enqueue($"[传送诊断] 收到 TeleportToQuestTarget map={mapIndex} x={x} y={y} monster={monsterIndex}");

            if (!HasQuestTarget(mapIndex, x, y, out uint npcObjectId, out bool questFinished))
            {
                MessageQueue.Enqueue($"[传送诊断] HasQuestTarget 返回 false, 不放行传送");
                return;
            }

            Map map = Envir.GetMap((int)mapIndex);
            if (map == null)
            {
                MessageQueue.Enqueue($"[传送诊断] GetMap({mapIndex}) 返回 null, 地图不存在");
                return;
            }

            Point p = new Point(x, y);

            // 怪物类目标：遍历怪物对象，传送到第一只活体怪物旁边，避免落在刷点却附近无怪
            if (monsterIndex > 0)
            {
                Point? live = FindLiveMonsterSpot(map, monsterIndex);
                if (live != null)
                {
                    MessageQueue.Enqueue($"[传送诊断] 找到活体怪物, 传送到 ({live.Value.X},{live.Value.Y})");
                    p = live.Value;
                }
            }

            if (!map.ValidPoint(p))
            {
                Point? near = FindNearbyValid(map, x, y);
                if (near == null)
                {
                    MessageQueue.Enqueue($"[传送诊断] ({x},{y}) 非法且无附近合法点, map={mapIndex}");
                    return;
                }
                MessageQueue.Enqueue($"[传送诊断] ({x},{y}) 非法, 改用附近点 ({near.Value.X},{near.Value.Y})");
                p = near.Value;
            }

            // 已在目标点同图且足够近：无需传送（避免"明明就在目标旁还被传送"）。
            // 注意不能用 Globals.DataRange(=16，交互范围)：玩家接任务时站在发放 NPC 旁，
            // 目标刷点常在十几格内，会被误判为"已到达"而点击无反应（如"商队的求救"的鸡刷点）。
            // 这里用与 HasQuestTarget 一致的 3 格作为"确实已到目标点"的判定。
            const int ArrivedRange = 3;
            bool alreadyThere = (CurrentMap == map) && Functions.InRange(CurrentLocation, p, ArrivedRange);
            if (!alreadyThere)
            {
                MessageQueue.Enqueue($"[传送诊断] 执行传送 -> map={mapIndex} ({p.X},{p.Y})");
                Teleport(map, p);
            }
            else
            {
                MessageQueue.Enqueue($"[传送诊断] 已在目标 3 格内 ({CurrentLocation.X},{CurrentLocation.Y})，不传送");
            }

            // 打开面板：直接用服务器自身任务数据解析出的 NPC 运行时 ObjectID，按 ID 精确 CallNPC，
            // 不再靠坐标猜"附近哪个 NPC"——避免紧邻 NPC 串台（如工匠大娘/传送员）。
            // 怪物类目标 npcObjectId 为 0，不会误开面板。
            if (npcObjectId != 0)
            {
                NPCObject targetNpc = Envir.NPCs.FirstOrDefault(n => n.ObjectID == npcObjectId);
                if (targetNpc != null && !Dead)
                {
                    // 原始传奇 checknpcTaskDialog：只有任务已完成待交(state=Finish)才弹任务面板；
                    // 进行中(state=Accepted，如"买一本技能书")仍走普通 NPC 面板，让玩家用商店功能。
                    CallNPC(targetNpc.ObjectID, NPCScript.MainKey, openQuestPanel: questFinished);
                }
            }
        }

        private Point? FindLiveMonsterSpot(Map map, uint monsterIndex)
        {
            // 遍历当前地图的全部怪物对象，传送到第一只匹配的活体怪物旁边
            List<MapObject> monsters = Envir.GetObjects(map.Info.Index, ObjectType.Monster);
            for (int i = 0; i < monsters.Count; i++)
            {
                MapObject ob = monsters[i];
                if (ob == null || ob.Dead) continue;
                MonsterObject mon = ob as MonsterObject;
                if (mon == null || mon.Info == null || mon.Info.Index != (int)monsterIndex) continue;

                // 传送到第一只活体怪物旁边（FindNearbyValid 从半径1开始，不会踩在怪物格上）
                Point? spot = FindNearbyValid(map, mon.CurrentLocation.X, mon.CurrentLocation.Y);
                if (spot != null) return spot;
                return mon.CurrentLocation;
            }
            return null;
        }

        // 校验 (map,x,y) 是否匹配玩家当前任务目标；若匹配且目标是 NPC，则一并通过 out 返回
        // 该目标对应的 NPC 运行时 ObjectID（取自任务数据本身，无需客户端额外传字段）。
        // 交付/返回类目标用 FinishNpcObjectID，接取/寻找类目标用起始 NpcObjectID。
        // questFinished 输出目标任务的待交状态(state=Finish)，用于 checknpcTaskDialog 面板判定。
        private bool HasQuestTarget(uint mapIndex, int x, int y, out uint npcObjectId, out bool questFinished)
        {
            npcObjectId = 0;
            questFinished = false;
            MessageQueue.Enqueue($"[传送诊断] HasQuestTarget 开始, CurrentQuests.Count={CurrentQuests.Count}, 请求 map={mapIndex} x={x} y={y}");
            for (int i = 0; i < CurrentQuests.Count; i++)
            {
                QuestProgressInfo quest = CurrentQuests[i];
                if (quest == null) continue;
                // 不跳过 Completed：已完成待交任务的"去找 NPC"目标正是 Completed 时才写入 Targets，
                // 需要允许玩家免费传送到交任务 NPC。任务交完后即移出 CurrentQuests，无副作用。
                if (quest.Targets == null || quest.Targets.Count == 0)
                    quest.UpdateTasks();
                if (quest.Targets == null) continue;

                MessageQueue.Enqueue($"[传送诊断]   任务[{i}] Index={quest.Index} Completed={quest.Completed} Targets.Count={quest.Targets.Count}");
                for (int j = 0; j < quest.Targets.Count; j++)
                {
                    QuestTaskTarget t = quest.Targets[j];
                    if (t == null) continue;
                    // NPC 目标必须严格对齐坐标（±1）：任务链里常有紧邻 NPC（比奇老兵 332,269 / 传送员 334,267），
                    // ±3 会串台打开旁边那个 NPC 的面板。怪物刷点仍用 ±3（活体位置可能偏移）。
                    int tol = t.Type == QuestTargetType.NPC ? 1 : 3;
                    bool match = t.MapIndex == mapIndex && Math.Abs(t.X - x) <= tol && Math.Abs(t.Y - y) <= tol;
                    MessageQueue.Enqueue($"[传送诊断]     目标[{j}] map={t.MapIndex} x={t.X} y={t.Y} type={t.Type} match={match}");
                    if (match)
                    {
                        // 仅 NPC 类目标需要打开面板；怪物类目标（打怪/收集）不打扰附近 NPC。
                        if (t.Type == QuestTargetType.NPC)
                        {
                            NPCObject targetNPC = null;
                            bool originQuest = quest.Info != null && !string.IsNullOrEmpty(quest.Info.source);
                            Point targetPos = new Point(x, y);

                            // 定位一律用 tnpc 策划索引 + 当前地图 + 坐标（ObjectID 走独立字段）。
                            if (originQuest && quest.Info.tnpc > 0)
                                targetNPC = Envir.NPCs.FirstOrDefault(n => n != null && n.Info != null && n.Info.Index == quest.Info.tnpc
                                                                          && n.CurrentMap != null && n.CurrentMap.Info != null
                                                                          && n.CurrentMap.Info.Index == (int)mapIndex
                                                                          && Functions.InRange(n.CurrentLocation, targetPos, 2));

                            // 老脚本任务：FinishNpcIndex / NpcIndex 同样是策划表 Index，按 Info.Index 精确匹配。
                            if (targetNPC == null && quest.Info != null)
                            {
                                uint objectID = quest.Completed ? quest.Info.FinishNpcObjectID : quest.Info.NpcObjectID;
                                targetNPC = objectID == 0 ? null : Envir.NPCs.FirstOrDefault(n => n != null && n.ObjectID == objectID && n.CurrentMap != null && n.CurrentMap.Info != null && n.CurrentMap.Info.Index == (int)mapIndex);
                                uint configIndex = quest.Completed ? quest.Info.FinishNpcIndex : quest.Info.NpcIndex;
                                targetNPC = Envir.NPCs.FirstOrDefault(n => n != null && n.Info != null && n.Info.Index == configIndex
                                                                          && n.CurrentMap != null && n.CurrentMap.Info != null
                                                                          && n.CurrentMap.Info.Index == (int)mapIndex
                                                                          && Functions.InRange(n.CurrentLocation, targetPos, 2));
                            }

                            if (targetNPC != null) npcObjectId = targetNPC.ObjectID;
                            MessageQueue.Enqueue($"[传送诊断]     解析面板NPC: name={targetNPC?.Name ?? "null"} objectID={npcObjectId} 策划表Index={quest.Info?.tnpc}");
                            questFinished = quest.Completed;
                        }
                        return true;
                    }
                }
            }
            MessageQueue.Enqueue($"[传送诊断] 未匹配到任何任务目标");
            return false;
        }

        private Point? FindNearbyValid(Map map, int x, int y)
        {
            for (int r = 1; r <= 12; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;

                        Point q = new Point(x + dx, y + dy);
                        if (map.ValidPoint(q))
                            return q;
                    }
                }
            }
            return null;
        }

        public void SearchMap(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 3) return;

            S.SearchMapResult p = new S.SearchMapResult();

            Map map = Envir.GetWorldMap(text);
            if (map != null)
            {
                CheckMapInfo(map.Info);
                p.MapIndex = map.Info.Index;
                Enqueue(p);
                return;
            }

            NPCObject npc = Envir.GetWorldMapNPC(text);
            if (npc != null)
            {
                CheckMapInfo(npc.CurrentMap.Info);
                p.MapIndex = npc.CurrentMap.Info.Index;
                p.NPCIndex = npc.ObjectID;
                Enqueue(p);
                return;
            }

            Enqueue(p);
            return;
        }
        private bool IsGroupMember(MapObject player)
        {
            if (player.Race != ObjectType.Player) return false;
            return GroupMembers != null && GroupMembers.Contains(player);
        }
        public override bool CanGainGold(uint gold)
        {
            return (ulong)gold + Info.Gold <= uint.MaxValue;
        }
        public override void WinGold(uint gold)
        {
            if (GroupMembers == null)
            {
                GainGold(gold);
                return;
            }

            uint count = 0;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject player = GroupMembers[i];
                if (player.CurrentMap == CurrentMap && Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) && !player.Dead)
                    count++;
            }

            if (count == 0 || count > gold)
            {
                GainGold(gold);
                return;
            }
            gold = gold / count;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject player = GroupMembers[i];
                if (player.CurrentMap == CurrentMap && Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) && !player.Dead)
                    player.GainGold(gold);
            }
        }
        public void GainGold(uint gold)
        {
            if (gold == 0) return;

            if (((UInt64)Info.Gold + gold) > uint.MaxValue)
                gold = uint.MaxValue - Info.Gold;

            Info.Gold += gold;

            Enqueue(new S.GainedGold { Gold = gold });
        }
        public void GainCredit(uint credit)
        {
            if (credit == 0) return;

            if (((UInt64)Info.Credit + credit) > uint.MaxValue)
                credit = uint.MaxValue - Info.Credit;

            Info.Credit += credit;

            Enqueue(new S.GainedCredit { Credit = credit });
        }
        public void GainLegendCoin(uint coin)
        {
            if (coin == 0) return;

            if (((UInt64)Info.LegendCoin + coin) > uint.MaxValue)
                coin = uint.MaxValue - Info.LegendCoin;

            Info.LegendCoin += coin;

            Enqueue(new S.GainedLegendCoin { LegendCoin = coin });
        }
        public void GainItemMail(UserItem item, int reason)
        {
            Envir.MailCharacter(Info, item: item, reason: reason);
        }
        public bool CanRemoveItem(MirGridType grid, UserItem item)
        {
            //Item  Stuck

            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (!CanAccessStorage()) return false;
                    array = Account.Storage;
                    break;
                case MirGridType.HeroInventory:
                    array = CurrentHero.Inventory;
                    break;
                default:
                    return false;
            }

            if (RidingMount && item.Info.Type != ItemType.Torch)
            {
                return false;
            }

            return FreeSpace(array) > 0;
        }
        public bool CheckQuestItem(UserItem uItem, ushort count)
        {
            foreach (var item in Info.QuestInventory.Where(item => item != null && item.Info == uItem.Info))
            {
                if (count > item.Count)
                {
                    count -= item.Count;
                    continue;
                }

                if (count > item.Count) continue;
                count = 0;
                break;
            }

            return count <= 0;
        }
        public bool CanGainQuestItem(UserItem item)
        {
            if (FreeSpace(Info.QuestInventory) > 0) return true;

            if (item.Info.StackSize > 1)
            {
                ushort count = item.Count;

                for (int i = 0; i < Info.QuestInventory.Length; i++)
                {
                    UserItem bagItem = Info.QuestInventory[i];

                    if (bagItem.Info != item.Info) continue;

                    if (bagItem.Count + count <= bagItem.Info.StackSize) return true;

                    count -= (ushort)(bagItem.Info.StackSize - bagItem.Count);
                }
            }

            ReceiveChat(LanguageMgr.Get("CannotCarryMoreQuestItems"), ChatType.System);

            return false;
        }
        public void GainQuestItem(UserItem item)
        {
            CheckItem(item);

            UserItem clonedItem = item.Clone();

            Enqueue(new S.GainedQuestItem { Item = clonedItem });

            AddQuestItem(item);
        }
        public void TakeQuestItem(ItemInfoTable uItem, ushort count)
        {
            for (int o = 0; o < Info.QuestInventory.Length; o++)
            {
                UserItem item = Info.QuestInventory[o];
                if (item == null) continue;
                if (item.Info != uItem) continue;

                if (count > item.Count)
                {
                    Enqueue(new S.DeleteQuestItem { UniqueID = item.UniqueID, Count = item.Count });
                    Info.QuestInventory[o] = null;

                    count -= item.Count;
                    continue;
                }

                Enqueue(new S.DeleteQuestItem { UniqueID = item.UniqueID, Count = count });

                if (count == item.Count)
                    Info.QuestInventory[o] = null;
                else
                    item.Count -= count;
                break;
            }
        }

        public void RequestChatItem(ulong id)
        {
            //Enqueue(new S.ChatItemStats { ChatItemId = id, Stats = whatever });
        }

        public override void ReceiveChat(string text, ChatType type)
        {
            Enqueue(new S.Chat { Message = text, Type = type });
        }
        public void ReceiveOutputMessage(string text, OutputMessageType type)
        {
            Enqueue(new S.SendOutputMessage { Message = text, Type = type });
        }
        // 【门已移除】原 Opendoor(byte) 删除：原始传奇地图数据不含门信息，服务端不再有门状态机。

    }
}