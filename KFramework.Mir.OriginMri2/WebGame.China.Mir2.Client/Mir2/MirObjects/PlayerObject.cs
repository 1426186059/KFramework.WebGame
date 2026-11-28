using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using Client.MirControls;
using S = ServerPackets;
using C = ClientPackets;
using Client.MirScenes.Dialogs;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    public partial class PlayerObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Player; }
        }

        public override bool Blocking
        {
            get { return !Dead; }
        }

        public MirGender Gender;
        public MirClass Class;
        public byte Hair;
        public ushort Level;

        public MLibrary WeaponLibrary1, WeaponEffectLibrary1, WeaponLibrary2, HairLibrary, WingLibrary, MountLibrary;

        // 原始传奇玩家角色模型号（按 职业+性别 映射，参考模型 9027）。0 = 不走 origin（回退 .Lib 分层绘制）。
        protected int _originModel;

        // 原始传奇 skillEffects.delaytime 以“施法起始时刻”为基准；命中特效在动画结束帧才创建，
        // 用这两个字段把基准拉回施法瞬间，避免额外多等一个 delaytime（雷电术 400ms）。
        private long _originCastTime;
        private Spell _originCastSpell;

        // 野蛮冲撞整段格数：服务端一次回终点包，客户端用它在一次 action4 动画内从起点插值到终点。
        public int DashSteps;
        // 起手施法时的真实起点（服务端终点包到达前，CurrentLocation 可能已被心跳同步到终点）。
        public Point DashStartLocation;

        // 各层【页游 id】（服务端在 ObjectPlayer 下发）。0 = 走裸身/不绘制回退。
        // 身体/武器来自 cm.item[col17]；装扮类(装扮/幻武/足迹/称号/徽记/法阵/旋光)来自 cm.title[id].model。
        protected int _pgArmour, _pgWeapon, _pgWing, _pgMount;
        private int _pgZhuangBan, _pgHuanWu, _pgFoot, _pgTitle, _pgHuiJi, _pgZhenfa, _pgXuanGuang;
        // 页游装饰装备层（cm.item[type] 角色精灵）：斗笠/面纱/玉佩/勋章/护盾/护肩/披风/疗伤石。
        protected int _pgDouLi, _pgMianSha, _pgYuPei, _pgXunZhang, _pgHuDun, _pgHuJian, _pgPiFeng, _pgLiaoShang;

        // 原始页游玩家外观是【分层复合精灵】（仿 animal.init2 / updateEquipments）：
        //   坐骑(底) → 翅膀(后) → 身体 → 武器 → 帽/时装(前)
        // 每层各自一个 origin model/{n}，坐标系统一，叠在 DrawLocation 即合成。
        // 身体模型（经逆向页游 main.min.js 确认）：身体 = 穿了盔甲/时装就用其模型，否则裸身 男=9001 / 女=9002。
        //   页游原码：l = null!=i && i>0 ? i : 2==t.sex ? 9002 : 9001   —— 仅按【性别】，不按职业！
        //   职业差异由所穿盔甲的 cm.item[col17] 体现（见下方 GetModel(_pgArmour)），故裸身不需要职业分支。
        // 穿衣时身体 = cm.item[盔甲id][17]；武器 = cm.item[武器id][17]；武器特效 = col18；翅膀 = cm.ring[wingId].model；坐骑 = fashionMount。

        // 已记录过“身体模型缺失、回落裸身”的模型号，避免每帧刷日志。
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte> _loggedBodyFallback = new();

        // 身体层：装扮 type0（时装身体，覆盖盔甲） > 盔甲槽 cm.item[col17] > 裸身 9001(男)/9002(女)。
        public int Armour, Weapon, WeaponEffect, ArmourOffSet, HairOffSet, WeaponOffSet, WingOffset, MountOffset;

        public int DieSound, FlinchSound, AttackSound;


        public FrameSet Frames;
        public Frame Frame, WingFrame;
        public int FrameIndex, FrameInterval, EffectFrameIndex, EffectFrameInterval, SlowFrameIndex;
        public byte SkipFrameUpdate = 0;

        public bool HasClassWeapon
        {
            get
            {
                switch (Weapon / Globals.ClassWeaponCount)
                {
                    default:
                        return Class == MirClass.Wizard || Class == MirClass.Warrior || Class == MirClass.Taoist;
                    case 1:
                        return Class == MirClass.Assassin;
                    case 2:
                        return Class == MirClass.Archer;
                }
            }
        }

        public bool HasFishingRod
        {
            get
            {
                return Globals.FishingRodShapes.Contains(Weapon);
            }
        }

        public Spell Spell;
        public byte SpellLevel;
        public bool Cast;
        public uint TargetID;
        public List<uint> SecondaryTargetIDs;
        public Point TargetPoint;

        public bool MagicShield;
        public Effect ShieldEffect;

        public bool ElementalBarrier;
        public Effect ElementalBarrierEffect;

        public byte WingEffect;
        private short StanceDelay = 2500;

        //ArcherSpells - Elemental system
        public bool ElementalBuff;
        public bool Concentrating;
        public InterruptionEffect ConcentratingEffect;
        public bool ConcentrateInterrupted;
        public bool HasElements;
        public bool ElementCasted;
        public int ElementEffect;//hold orb count for player(object) load
        public int ElementsLevel;
        public int ElementOrbMax;
        //Elemental system END

        public SpellEffect CurrentEffect;

        public bool RidingMount, Sprint, FastRun, Fishing, FoundFish;
        public long StanceTime, MountTime, FishingTime;
        public long BlizzardStopTime, ReincarnationStopTime, SlashingBurstTime;

        public short MountType = -1, TransformType = -1;

        public string GuildName;
        public string GuildRankName;

        // 头顶称号/阵法名字（占位字段：服务端当前只下发 OriginTitleID/OriginZhenfaID 外观 ID，没有名字文本；
        // 后续接入时按 ID 查配置填入即可，本轮 DrawName 只画行会名+玩家名，不显示这两个）
        public string ChengHaoName;
        public string ZhenfaName;

        public Point FishingPoint;

        public LevelEffects LevelEffects;

        public PlayerObject() { }
        public PlayerObject(uint objectID) : base(objectID)
        {
            Frames = FrameSet.Player;
        }

        public void Load(S.ObjectPlayer info)
        {
            Name = info.Name;
            NameColour = info.NameColour;
            GuildName = info.GuildName;
            GuildRankName = info.GuildRankName;
            Class = info.Class;
            Gender = info.Gender;
            _pgArmour = info.OriginArmourID;
            _pgWeapon = info.OriginWeaponID;
            _pgWing = info.OriginWingID;
            _pgMount = info.OriginMountID;
            _pgZhuangBan = info.OriginZhuangBanID;
            _pgHuanWu = info.OriginHuanWuID;
            _pgFoot = info.OriginFootID;
            _pgTitle = info.OriginTitleID;
            _pgHuiJi = info.OriginHuiJiID;
            _pgZhenfa = info.OriginZhenfaID;
            _pgXuanGuang = info.OriginXuanGuangID;
            _pgDouLi = info.OriginDouLiID;
            _pgMianSha = info.OriginMianShaID;
            _pgYuPei = info.OriginYuPeiID;
            _pgXunZhang = info.OriginXunZhangID;
            _pgHuDun = info.OriginHuDunID;
            _pgHuJian = info.OriginHuJianID;
            _pgPiFeng = info.OriginPiFengID;
            _pgLiaoShang = info.OriginLiaoShangID;
            _originModel = PlayerBodyModelResolved();
            Level = info.Level;

            CurrentLocation = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);

            Direction = info.Direction;
            Hair = info.Hair;

            Weapon = info.Weapon;
			WeaponEffect = info.WeaponEffect;
			Armour = info.Armour;
            Light = info.Light;

            Poison = info.Poison;

            Dead = info.Dead;
            Hidden = info.Hidden;

            WingEffect = info.WingEffect;
            CurrentEffect = info.Effect;

            MountType = info.MountType;
            RidingMount = info.RidingMount;

            Fishing = info.Fishing;

            TransformType = info.TransformType;

            SetLibraries();

            if (Dead) ActionFeed.Add(new QueuedAction { Action = MirAction.Dead, Direction = Direction, Location = CurrentLocation });
            if (info.Extra) Effects.Add(new Effect(Libraries.Magic2, 670, 10, 800, this));

            ElementEffect = (int)info.ElementOrbEffect;
            ElementsLevel = (int)info.ElementOrbLvl;
            ElementOrbMax = (int)info.ElementOrbMax;

            Buffs = info.Buffs;

            LevelEffects = info.LevelEffects;

            ProcessBuffs();

            SetAction();

            SetEffects();
        }
        public void Update(S.PlayerUpdate info)
        {
            Weapon = info.Weapon;
			WeaponEffect = info.WeaponEffect;
			Armour = info.Armour;
            Light = info.Light;
            WingEffect = info.WingEffect;

            SetLibraries();
            SetEffects();
        }

        public override bool ShouldDrawHealth()
        {
            if (GroupDialog.GroupList.Contains(Name) || this == User)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void ProcessBuffs()
        {
            for (int i = 0; i < Buffs.Count; i++)
            {
                AddBuffEffect(Buffs[i]);
            }
        }

        public void MountUpdate(S.MountUpdate info)
        {
            MountType = info.MountType;
            RidingMount = info.RidingMount;

            QueuedAction action = new QueuedAction { Action = MirAction.Standing, Direction = Direction, Location = CurrentLocation };
            ActionFeed.Insert(0, action);

            MountTime = CMain.Time;

            if (MountType < 0)
                GameScene.Scene.MountDialog.Hide();

            SetLibraries();
            SetEffects();

            PlayMountSound();
        }

        public void FishingUpdate(S.FishingUpdate p)
        {
            if (Fishing != p.Fishing)
            {
                MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, p.FishingPoint);

                if (p.Fishing)
                {        
                    QueuedAction action = new QueuedAction { Action = MirAction.FishingCast, Direction = dir, Location = CurrentLocation };
                    ActionFeed.Add(action);
                }
                else
                {
                    QueuedAction action = new QueuedAction { Action = MirAction.FishingReel, Direction = dir, Location = CurrentLocation };
                    ActionFeed.Add(action);
                }

                Fishing = p.Fishing;
                SetLibraries();
            }

            if (!HasFishingRod)
            {
                GameScene.Scene.FishingDialog.Hide();
            }          

            FishingPoint = p.FishingPoint;
            FoundFish = p.FoundFish;
        }


        public virtual void SetLibraries()
        {
            //fishing broken
            //10
            //11
            //12
            //13

            //almost all broken
            //20 - black footballer - 791
            //21 - red footballer - 791
            //22 - blue footballer - 791
            //23 - green footballer - 791
            //24 - red2 footballer - 791

            bool altAnim = false;

            bool showMount = true;
            bool showFishing = true;

            if (TransformType > -1)
            {
                
                switch (TransformType)
                {
                    case 4:
                    case 5:
                    case 7:
                    case 8:                
                    case 26:
                    case 28:
                    case 29:
                    case 30:
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                    case 35:
                    case 36:
                    case 37:
                    case 38:
                        showFishing = false;
                        break;
                    case 6:
                    case 9:
                        showMount = false;
                        showFishing = false;
                        break;
                    default:
                        break;
                }

                switch (CurrentAction)
                {
                    case MirAction.Standing:
                    case MirAction.Jump:
                        Frames.TryGetValue(MirAction.Standing, out Frame);
                        break;
                    case MirAction.Walking:
                    case MirAction.WalkingBow:
                        Frames.TryGetValue(MirAction.Walking, out Frame);
                        break;
                    case MirAction.Running:
                    case MirAction.RunningBow:
                        Frames.TryGetValue(MirAction.Running, out Frame);
                        break;
                    case MirAction.Attack1:
                    case MirAction.Attack2:
                    case MirAction.Attack3:
                    case MirAction.Attack4:
                    case MirAction.AttackRange1:
                    case MirAction.AttackRange2:
                    case MirAction.AttackRange3:
                        Frames.TryGetValue(MirAction.Attack1, out Frame);
                        break;
                }

                if (MountType > 6 && RidingMount)
                {
                    ArmourOffSet = -416;
                    BodyLibrary = TransformType < Libraries.TransformMounts.Length ? Libraries.TransformMounts[TransformType] : Libraries.TransformMounts[0];
                }
                else
                {
                    ArmourOffSet = 0;
                    BodyLibrary = TransformType < Libraries.Transform.Length ? Libraries.Transform[TransformType] : Libraries.Transform[0];
                }

                HairLibrary = null;
                WeaponLibrary1 = null;
                WeaponLibrary2 = null;

                if (TransformType == 19)
                {
                    WingEffect = 2;
                    WingLibrary = WingEffect - 1 < Libraries.TransformEffect.Length ? Libraries.TransformEffect[WingEffect - 1] : null;
                }
                else
                {
                    WingLibrary = null;
                }

                HairOffSet = 0;
                WeaponOffSet = 0;
                WingOffset = 0;
                MountOffset = 0;

            }
            else
            {

                switch (Class)
                {
                    case MirClass.Archer:

                        if (HasClassWeapon)
                        {
                            switch (CurrentAction)
                            {
                                case MirAction.Walking:
                                case MirAction.Running:
                                case MirAction.AttackRange1:
                                case MirAction.AttackRange2:
                                    altAnim = true;
                                    break;
                            }
                        }

                        if (CurrentAction == MirAction.Jump) altAnim = true;


                        if (altAnim)
                        {
                            BodyLibrary = Armour < Libraries.ARArmours.Length ? Libraries.ARArmours[Armour] : Libraries.ARArmours[0];
                            HairLibrary = Hair < Libraries.ARHair.Length ? Libraries.ARHair[Hair] : null;
                        }
                        else
                        {
                            BodyLibrary = Armour < Libraries.CArmours.Length ? Libraries.CArmours[Armour] : Libraries.CArmours[0];
                            HairLibrary = Hair < Libraries.CHair.Length ? Libraries.CHair[Hair] : null;
                        }

                        if (HasClassWeapon)
                        {
                            int Index = Weapon - 200;

                            if (altAnim)
                                WeaponLibrary2 = Index < Libraries.ARWeaponsS.Length ? Libraries.ARWeaponsS[Index] : null;
                            else
                                WeaponLibrary2 = Index < Libraries.ARWeapons.Length ? Libraries.ARWeapons[Index] : null;

                            WeaponLibrary1 = null;
                        }
                        else
                        {
							if (Weapon >= 0)
							{
								WeaponLibrary1 = Weapon < Libraries.CWeapons.Length ? Libraries.CWeapons[Weapon] : null;
								if (WeaponEffect > 0)
									WeaponEffectLibrary1 = WeaponEffect < Libraries.CWeaponEffect.Length ? Libraries.CWeaponEffect[WeaponEffect] : null;
								else
									WeaponEffectLibrary1 = null;

                                WeaponLibrary2 = null;
                            }
							else
							{
								WeaponLibrary1 = null;
								WeaponEffectLibrary1 = null;
								WeaponLibrary2 = null;
							}
						}

                        if (WingEffect > 0 && WingEffect < 100)
                        {
                            if (altAnim)
                                WingLibrary = (WingEffect - 1) < Libraries.ARHumEffect.Length ? Libraries.ARHumEffect[WingEffect - 1] : null;
                            else
                                WingLibrary = (WingEffect - 1) < Libraries.CHumEffect.Length ? Libraries.CHumEffect[WingEffect - 1] : null;
                        }

                        ArmourOffSet = Gender == MirGender.Male ? 0 : altAnim ? 352 : 808;
                        HairOffSet = Gender == MirGender.Male ? 0 : altAnim ? 352 : 808;
                        WeaponOffSet = Gender == MirGender.Male ? 0 : altAnim ? 352 : 416;
                        WingOffset = Gender == MirGender.Male ? 0 : altAnim ? 352 : 840;
                        MountOffset = 0;

                        break;


                    case MirClass.Assassin:

                        if (HasClassWeapon || Weapon < 0)
                        {
                            switch (CurrentAction)
                            {
                                case MirAction.Standing:
                                case MirAction.Stance:
                                case MirAction.Walking:
                                case MirAction.Running:
                                case MirAction.Die:
                                case MirAction.Struck:
                                case MirAction.Attack1:
                                case MirAction.Attack2:
                                case MirAction.Attack3:
                                case MirAction.Attack4:
                                case MirAction.Sneek:
                                case MirAction.Spell:
                                case MirAction.DashAttack:
                                    altAnim = true;
                                    break;
                            }
                        }

                        if (altAnim)
                        {
                            BodyLibrary = Armour < Libraries.AArmours.Length ? Libraries.AArmours[Armour] : Libraries.AArmours[0];
                            HairLibrary = Hair < Libraries.AHair.Length ? Libraries.AHair[Hair] : null;
                        }
                        else
                        {
                            BodyLibrary = Armour < Libraries.CArmours.Length ? Libraries.CArmours[Armour] : Libraries.CArmours[0];
                            HairLibrary = Hair < Libraries.CHair.Length ? Libraries.CHair[Hair] : null;
                        }

                        if (HasClassWeapon)
                        {
                            int Index = Weapon - 100;

                            WeaponLibrary1 = Index < Libraries.AWeaponsL.Length ? Libraries.AWeaponsR[Index] : null;
                            WeaponLibrary2 = Index < Libraries.AWeaponsR.Length ? Libraries.AWeaponsL[Index] : null;
                        }
                        else
                        {
							if (Weapon >= 0)
							{
								WeaponLibrary1 = Weapon < Libraries.CWeapons.Length ? Libraries.CWeapons[Weapon] : null;
								if (WeaponEffect > 0)
									WeaponEffectLibrary1 = WeaponEffect < Libraries.CWeaponEffect.Length ? Libraries.CWeaponEffect[WeaponEffect] : null;
								else
									WeaponEffectLibrary1 = null;

                                WeaponLibrary2 = null;
                            }
							else
							{
								WeaponLibrary1 = null;
								WeaponEffectLibrary1 = null;
								WeaponLibrary2 = null;
							}
						}

                        if (WingEffect > 0 && WingEffect < 100)
                        {
                            if (altAnim)
                                WingLibrary = (WingEffect - 1) < Libraries.AHumEffect.Length ? Libraries.AHumEffect[WingEffect - 1] : null;
                            else
                                WingLibrary = (WingEffect - 1) < Libraries.CHumEffect.Length ? Libraries.CHumEffect[WingEffect - 1] : null;
                        }

                        ArmourOffSet = Gender == MirGender.Male ? 0 : altAnim ? 512 : 808;
                        HairOffSet = Gender == MirGender.Male ? 0 : altAnim ? 512 : 808;
                        WeaponOffSet = Gender == MirGender.Male ? 0 : altAnim ? 512 : 416;
                        WingOffset = Gender == MirGender.Male ? 0 : altAnim ? 544 : 840;
                        MountOffset = 0;

                        break;


                    case MirClass.Warrior:
                    case MirClass.Taoist:
                    case MirClass.Wizard:

                        BodyLibrary = Armour < Libraries.CArmours.Length ? Libraries.CArmours[Armour] : Libraries.CArmours[0];
                        HairLibrary = Hair < Libraries.CHair.Length ? Libraries.CHair[Hair] : null;


						if (Weapon >= 0)
						{
							WeaponLibrary1 = Weapon < Libraries.CWeapons.Length ? Libraries.CWeapons[Weapon] : null;
							if (WeaponEffect > 0)
								WeaponEffectLibrary1 = WeaponEffect < Libraries.CWeaponEffect.Length ? Libraries.CWeaponEffect[WeaponEffect] : null;
							else
								WeaponEffectLibrary1 = null;
						}
						else
						{
							WeaponLibrary1 = null;
							WeaponEffectLibrary1 = null;
							WeaponLibrary2 = null;
						}


						if (WingEffect > 0 && WingEffect < 100)
                        {
                            WingLibrary = (WingEffect - 1) < Libraries.CHumEffect.Length ? Libraries.CHumEffect[WingEffect - 1] : null;
                        }

                        ArmourOffSet = Gender == MirGender.Male ? 0 : 808;
                        HairOffSet = Gender == MirGender.Male ? 0 : 808;
                        WeaponOffSet = Gender == MirGender.Male ? 0 : 416;
                        WingOffset = Gender == MirGender.Male ? 0 : 840;
                        MountOffset = 0;

                        break;
                }
            }

            //Harvest
            if (CurrentAction == MirAction.Harvest && TransformType < 0)
            {
                WeaponLibrary1 = 1 < Libraries.CWeapons.Length ? Libraries.CWeapons[1] : null;
            }

            //Mounts
            if (MountType > -1 && RidingMount && showMount)
            {
                MountLibrary = MountType < Libraries.Mounts.Length ? Libraries.Mounts[MountType] : null;
            }
            else
            {
                MountLibrary = null;
            }

            //Fishing
            if (HasFishingRod && showFishing)
            {
                if (CurrentAction == MirAction.FishingCast || CurrentAction == MirAction.FishingWait || CurrentAction == MirAction.FishingReel)
                {
                    WeaponLibrary1 = 0 < Libraries.Fishing.Length ? Libraries.Fishing[Weapon - 49] : null;
                    WeaponLibrary2 = null;
                    WeaponOffSet = -632;
                }
            }

            DieSound = Gender == MirGender.Male ? SoundList.MaleDie : SoundList.FemaleDie;
            FlinchSound = Gender == MirGender.Male ? SoundList.MaleFlinch : SoundList.FemaleFlinch;
        }

        public virtual void SetEffects()
        {
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                if (Effects[i] is SpecialEffect) Effects[i].Remove();
            }

            if (RidingMount) return;

            if (CurrentEffect == SpellEffect.MagicShieldUp)
            {
                if (ShieldEffect != null)
                {
                    ShieldEffect.Clear();
                    ShieldEffect.Remove();
                }

                MagicShield = true;
                Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
            }

            if (WingEffect >= 100)
            {
                switch(WingEffect)
                {
                    case 100: //Oma King Robe effect
                        Effects.Add(new SpecialEffect(Libraries.Effect, 352, 33, 3600, this, true, false, 0) { Repeat = true });
                        break;
						case 101://Black Dragon Armour  Fire effect
                        Effects.Add(new SpecialEffect(Libraries.CHumEffect[4], 0, 10, 1500, this, true, false, 0u)
                       {
                        Repeat = true
                        });
                      break;
                }
            }

			long delay = 5000;

            if (LevelEffects == LevelEffects.None) return;

            //Effects dependant on flags
            if (LevelEffects.HasFlag(LevelEffects.BlueDragon))
            {
                Effects.Add(new SpecialEffect(Libraries.Effect, 1210, 20, 3200, this, true, true, 1) { Repeat = true });
                SpecialEffect effect = new SpecialEffect(Libraries.Effect, 1240, 32, 4200, this, true, false, 1) { Repeat = true };
                effect.SetStart(CMain.Time);
                Effects.Add(effect);
            }

            if (LevelEffects.HasFlag(LevelEffects.RedDragon))
            {
                Effects.Add(new SpecialEffect(Libraries.Effect, 990, 20, 3200, this, true, true, 1) { Repeat = true });
                SpecialEffect effect = new SpecialEffect(Libraries.Effect, 1020, 32, 4200, this, true, false, 1) { Repeat = true, Delay = delay };
                effect.SetStart(CMain.Time + delay);
                Effects.Add(effect);
            }

            if (LevelEffects.HasFlag(LevelEffects.Mist))
            {
                Effects.Add(new SpecialEffect(Libraries.Effect, 296, 32, 3600, this, true, false, 1) { Repeat = true });
            }

            if (LevelEffects.HasFlag(LevelEffects.Rebirth1))
            {
                Effects.Add(new SpecialEffect(Libraries.Magic3, 6800, 20, 3600, this, true, false, 1) { Repeat = true });


            }

            if (LevelEffects.HasFlag(LevelEffects.Rebirth2))
            {
                Effects.Add(new SpecialEffect(Libraries.Magic3, 6870, 19, 3600, this, true, true, 1) { Repeat = true });
                SpecialEffect effect = new SpecialEffect(Libraries.Magic3, 6840, 22, 3600, this, true, false, 1) { Repeat = true, Delay = delay };
                effect.SetStart(CMain.Time + delay);
                Effects.Add(effect);
            }

            if (LevelEffects.HasFlag(LevelEffects.Rebirth3))
            {
                Effects.Add(new SpecialEffect(Libraries.Magic3, 6906, 19, 3600, this, true, false, 1) { Repeat = true, Delay = delay });
                SpecialEffect effect = new SpecialEffect(Libraries.Magic3, 6930, 29, 3600, this, true, true, 1) { Repeat = true };

                Effects.Add(effect);
            }

            if (LevelEffects.HasFlag(LevelEffects.NewBlue))
            {
                Effects.Add(new SpecialEffect(Libraries.Magic3, 7040, 31, 3600, this, true, false, 1) { Repeat = true, Delay = delay });
                SpecialEffect effect = new SpecialEffect(Libraries.Magic3,7080, 24, 3600, this, true, true, 1) { Repeat = true };

                Effects.Add(effect);
            }

            if (LevelEffects.HasFlag(LevelEffects.YellowDragon))
            {
                Effects.Add(new SpecialEffect(Libraries.Magic3, 7120, 31, 3600, this, true, false, 1) { Repeat = true, Delay = delay });
                SpecialEffect effect = new SpecialEffect(Libraries.Magic3, 7160, 24, 3600, this, true, true, 1) { Repeat = true };

                Effects.Add(effect);
            }

            if (LevelEffects.HasFlag(LevelEffects.Phoenix))
            {
                Effects.Add(new SpecialEffect(Libraries.Magic3, 6970, 26, 3600, this, true, false, 1) { Repeat = true, Delay = delay });
                SpecialEffect effect = new SpecialEffect(Libraries.Magic3, 7000, 21, 3600, this, true, true, 1) { Repeat = true };

                Effects.Add(effect);
            }
        }

        /// <summary>
        /// 原始传奇数据驱动技能特效：按 5config.skillEffects 播放 CDN 特效，替代 .Lib(Libraries.Magic*) 硬编码帧。
        /// 拆成起手(PlayOriginCastEffects)/命中飞行(PlayOriginHitEffects)两段，分别在施法动作起始/释放帧调用，
        /// 保证时序与原始一致。查不到 skillEffects 时返回 false，让调用方回退旧 switch。
        /// </summary>
        public override void Process()
        {
            bool update = CMain.Time >= NextMotion || GameScene.CanMove;

            if (this == User)
            {
                if (CMain.Time - GameScene.LastRunTime > 899)
                    GameScene.CanRun = false;
            }

            SkipFrames = this != User && ActionFeed.Count > 0;

            ProcessFrames();

            if (Frame == null)
            {
                DrawFrame = 0;
                DrawWingFrame = 0;
            }
            else
            {
                DrawFrame = Frame.Start + (Frame.OffSet * (byte)Direction) + FrameIndex;
                DrawWingFrame = Frame.EffectStart + (Frame.EffectOffSet * (byte)Direction) + EffectFrameIndex;
            }


            switch (CurrentAction)
            {
                case MirAction.Walking:
                case MirAction.Running:
                case MirAction.MountWalking:
                case MirAction.MountRunning:
                case MirAction.Pushed:
                case MirAction.DashL:
                case MirAction.DashR:
                case MirAction.Sneek:
                case MirAction.Jump:
                case MirAction.DashAttack:
                    if (Frame == null)
                    {
                        OffSetMove = Point.Zero;
                        Movement = CurrentLocation;
                        break;
                    }

                    var i = 0;
                    if (CurrentAction == MirAction.MountRunning) i = 3;
                    else if (CurrentAction == MirAction.Running) 
                        i = (Sprint && !Sneaking ? 3 : 2);
                    else i = 1;

                    if (CurrentAction == MirAction.Jump) i = -JumpDistance;
                    if (CurrentAction == MirAction.DashAttack) i = JumpDistance;

                    // 野蛮冲撞：一次动画走完整段，Movement 回退 DashSteps 格到冲撞起点。
                    if (CurrentAction == MirAction.DashL || CurrentAction == MirAction.DashR)
                    {
                        i = DashSteps;
                    }

                    Movement = Functions.PointMove(CurrentLocation, Direction, CurrentAction == MirAction.Pushed ? 0 : -i);

                    int count = Frame.Count;
                    int index = FrameIndex;

                    switch (Direction)
                    {
                        case MirDirection.Up:
                            OffSetMove = new Point(0, (int)((MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.UpRight:
                            OffSetMove = new Point((int)((-MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.Right:
                            OffSetMove = new Point((int)((-MapControl.CellWidth * i / (float)(count)) * (index + 1)), 0);
                            break;
                        case MirDirection.DownRight:
                            OffSetMove = new Point((int)((-MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((-MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.Down:
                            OffSetMove = new Point(0, (int)((-MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.DownLeft:
                            OffSetMove = new Point((int)((MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((-MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                        case MirDirection.Left:
                            OffSetMove = new Point((int)((MapControl.CellWidth * i / (float)(count)) * (index + 1)), 0);
                            break;
                        case MirDirection.UpLeft:
                            OffSetMove = new Point((int)((MapControl.CellWidth * i / (float)(count)) * (index + 1)), (int)((MapControl.CellHeight * i / (float)(count)) * (index + 1)));
                            break;
                    }

                    OffSetMove = new Point(OffSetMove.X % 2 + OffSetMove.X, OffSetMove.Y % 2 + OffSetMove.Y);
                    break;
                default:
                    OffSetMove = Point.Zero;
                    Movement = CurrentLocation;
                    break;
            }



            DrawY = Movement.Y > CurrentLocation.Y ? Movement.Y : CurrentLocation.Y;

            DrawLocation = new Point((Movement.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth, (Movement.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            DrawLocation.Offset(GlobalDisplayLocationOffset);

            if (this != User)
            {
                DrawLocation.Offset(User.OffSetMove);
                DrawLocation.Offset(-OffSetMove.X, -OffSetMove.Y);
            }

            if (BodyLibrary != null && update)
            {
                FinalDrawLocation = DrawLocation.Add(BodyLibrary.GetOffSet(DrawFrame));
                DisplayRectangle = new Rectangle(DrawLocation, BodyLibrary.GetTrueSize(DrawFrame).ToPoint());
            }

            for (int i = 0; i < Effects.Count; i++)
                Effects[i].Process();

            Color newColour = Poison switch
            {
                _ when (Poison & PoisonType.DelayedExplosion) == PoisonType.DelayedExplosion => CommonColor.Orange,
                _ when (Poison & (PoisonType.Paralysis | PoisonType.LRParalysis)) != 0 => CommonColor.Gray,
                _ when (Poison & PoisonType.Frozen) == PoisonType.Frozen => CommonColor.Blue,
                _ when (Poison & PoisonType.Blindness) == PoisonType.Blindness => CommonColor.MediumVioletRed,
                _ when (Poison & (PoisonType.Stun | PoisonType.Dazed)) != 0 => CommonColor.Yellow,
                _ when (Poison & PoisonType.Slow) == PoisonType.Slow => CommonColor.Purple,
                _ when (Poison & PoisonType.Bleeding) == PoisonType.Bleeding => CommonColor.DarkRed,
                _ when (Poison & PoisonType.Red) == PoisonType.Red => CommonColor.Red,
                _ when (Poison & PoisonType.Green) == PoisonType.Green => CommonColor.Green,
                _ => CommonColor.White
            };

            if (newColour != DrawColour)
            {
                DrawColour = newColour;
                GameScene.Scene.MapControl.TextureValid = false;
            }
        }
    }


    public class QueuedAction
    {
        public MirAction Action;
        public Point Location;
        public MirDirection Direction;
        public List<object> Params;
    }

}