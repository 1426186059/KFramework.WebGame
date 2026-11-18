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
    public class PlayerObject : MapObject
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
        private int _originModel;

        // 各层【页游 id】（服务端在 ObjectPlayer 下发）。0 = 走裸身/不绘制回退。
        // 身体/武器来自 cm.item[col17]；装扮类(装扮/幻武/足迹/称号/徽记/法阵/旋光)来自 cm.title[id].model。
        private int _pgArmour, _pgWeapon, _pgWing, _pgMount;
        private int _pgZhuangBan, _pgHuanWu, _pgFoot, _pgTitle, _pgHuiJi, _pgZhenfa, _pgXuanGuang;
        // 页游装饰装备层（cm.item[type] 角色精灵）：斗笠/面纱/玉佩/勋章/护盾/护肩/披风/疗伤石。
        private int _pgDouLi, _pgMianSha, _pgYuPei, _pgXunZhang, _pgHuDun, _pgHuJian, _pgPiFeng, _pgLiaoShang;

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
        private int PlayerBodyModelResolved()
        {
            int costume = Mir2OriginModelResolver.GetTitleModel(_pgZhuangBan, Gender == MirGender.Female, 0);
            if (costume > 0) return costume;                       // 时装身体（覆盖盔甲）
            int armour = Mir2OriginModelResolver.GetModel(_pgArmour, Gender == MirGender.Female, 0);
            if (armour > 0) return armour;                         // 普通盔甲（含职业差异）
            return Gender == MirGender.Female ? 9002 : 9001;       // 裸身：男 9001 / 女 9002（仅按性别）
        }
        // 本地玩家(UserObject)没有 ObjectPlayer 包，需从自身 Equipment 反推各层 origin 模型号（页游物品 id = ItemInfo.Index）。
        // 与服务端 ObjectPlayer.OriginArmourID = Equipment[Armour]?.Info.Index 同义；盔甲/武器/坐骑来自装备槽。
        // 翅膀/装扮等本服务端未在下发的 UserInformation 中追踪，回落 0（不绘制）。仅按传入 Equipment 解算；装备变化(SetSlots)时再次调用刷新。
        protected void SetUserOriginModels(UserItem[] equipment)
        {
            _pgArmour = equipment[(int)EquipmentSlot.Armour]?.Info?.Index ?? 0;   // 身体（穿盔甲用其模型，否则裸身）
            _pgWeapon = equipment[(int)EquipmentSlot.Weapon]?.Info?.Index ?? 0;  // 武器层
            _pgMount  = equipment[(int)EquipmentSlot.Mount]?.Info?.Index ?? 0;   // 坐骑层
            _originModel = PlayerBodyModelResolved();
            PreloadSelfModels();   // 出场（Loading）/换装即开始预载自己全部装备层（急队列，不等待绘制）
        }

        /// <summary>后台静默预载「自己」的全部装备层（身体/头发/护肩/武器/坐骑/装饰…），对齐原始 ActionLoader 的急队列：
        /// 出场即开始加载，不等待绘制；装备变化(SetSlots)时再次刷新。仅对 User 调用（this != User 直接返回）。</summary>
        private void PreloadSelfModels()
        {
            if (this != User) return;
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, _originModel, Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerHairModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerHuJianModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerHuiJiModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerWeaponModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerMountModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerZhenfaModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerFootModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerPiFengModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerDouLiModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerMianShaModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerYuPeiModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerXunZhangModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerHuDunModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerXuanGuangModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerTitleModel(), Mir2OriginObjectLibrary.PriorityUser);
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, PlayerWingModel(), Mir2OriginObjectLibrary.PriorityUser);
            // 裸身兜底模型（身体 CDN 缺失时回落 9001/9002）也预载，避免回退时再等待。
            Mir2OriginObjectLibrary.PreloadModel(Mir2ObjectKind.Player, Gender == MirGender.Female ? 9002 : 9001, Mir2OriginObjectLibrary.PriorityUser);
        }

        /// <summary>后台懒预取：把自己当前动作的各方向暖好，转身/换动作不跳帧（低优先级，空闲才下）。</summary>
        public override void PrefetchBackground()
        {
            if (_originModel > 0)
                Mir2OriginObjectLibrary.PreloadAction(Mir2ObjectKind.Player, _originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), Mir2OriginObjectLibrary.PriorityBackground);
        }
        // 武器层：优先【幻武 type1】（时装武器，覆盖普通武器）；否则武器槽 cm.item[col17]；无则 0 不绘制。
        private int PlayerWeaponModel()
            => Mir2OriginModelResolver.GetTitleModel(_pgHuanWu, Gender == MirGender.Female,
                Mir2OriginModelResolver.GetModel(_pgWeapon, Gender == MirGender.Female, 0));
        // 翅膀层模型号：本客户端 Looks_Wings 是另一套（byte，且页游羽翼在 cm.title type13），暂无「Looks_Wings → 页游羽翼id」映射，暂返回 0。
        // 待接 cm.title[yuyiId].model 后即可点亮。
        private int PlayerWingModel() => 0;
        // 坐骑层模型号：页游把 fashionMount 直接作为 origin 模型号传给 animal.init2（main.min.js 实锤），OriginMountID = 服务端 Mount.MountType 即模型号。
        private int PlayerMountModel() => _pgMount;
        // 以下装扮类均来自 cm.title[id].model（type 区分）：称号3 / 足迹2 / 徽记4 / 法阵5 / 旋光7。id 由服务端下发（当前默认 0）。
        private int PlayerTitleModel() => Mir2OriginModelResolver.GetTitleModel(_pgTitle, Gender == MirGender.Female, 0);    // 称号
        private int PlayerFootModel() => Mir2OriginModelResolver.GetTitleModel(_pgFoot, Gender == MirGender.Female, 0);      // 足迹
        private int PlayerHuiJiModel() => Mir2OriginModelResolver.GetTitleModel(_pgHuiJi, Gender == MirGender.Female, 0);    // 徽记
        private int PlayerZhenfaModel() => Mir2OriginModelResolver.GetTitleModel(_pgZhenfa, Gender == MirGender.Female, 0);  // 法阵
        private int PlayerXuanGuangModel() => Mir2OriginModelResolver.GetTitleModel(_pgXuanGuang, Gender == MirGender.Female, 0); // 旋光
        // 以下装饰装备层均来自 cm.item[type] 角色精灵（type：斗笠50/勋章51/玉佩52/护盾53/护肩54/披风55/面纱406）。id 由服务端下发（默认 0）。
        private int PlayerDouLiModel() => Mir2OriginModelResolver.GetModel(_pgDouLi, Gender == MirGender.Female, 0);      // 斗笠（头部）
        private int PlayerMianShaModel() => Mir2OriginModelResolver.GetModel(_pgMianSha, Gender == MirGender.Female, 0);   // 面纱（面部）
        private int PlayerYuPeiModel() => Mir2OriginModelResolver.GetModel(_pgYuPei, Gender == MirGender.Female, 0);        // 玉佩
        private int PlayerXunZhangModel() => Mir2OriginModelResolver.GetModel(_pgXunZhang, Gender == MirGender.Female, 0);  // 勋章（荣誉勋章）
        private int PlayerHuDunModel() => Mir2OriginModelResolver.GetModel(_pgHuDun, Gender == MirGender.Female, 0);        // 护盾
        private int PlayerHuJianModel() => Mir2OriginModelResolver.GetModel(_pgHuJian, Gender == MirGender.Female, 0);      // 护肩
        private int PlayerPiFengModel() => Mir2OriginModelResolver.GetModel(_pgPiFeng, Gender == MirGender.Female, 0);      // 披风
        private int PlayerLiaoShangModel() => Mir2OriginModelResolver.GetModel(_pgLiaoShang, Gender == MirGender.Female, 0); // 疗伤石（页游多为纯属性，无精灵）
        // 头发层：原始传奇里头发是叠在（光头）身体底模之上的独立 origin 模型层；已确认 model/9498 男、model/9499 女（CDN 版本 10.1393.1，含完整角色动画 1/2/3/4/5/10/21/23/99）。
        // 与经典 Mir2 一致：身体底模（9001/9002 等）不含头发，头发需单独叠绘。本版本 CDN 仅两套头发模型，按性别取（Hair 发型字段暂不参与模型选择）。
        private int PlayerHairModel() => Gender == MirGender.Female ? 9499 : 9498;
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

                    Movement = Functions.PointMove(CurrentLocation, Direction, CurrentAction == MirAction.Pushed ? 0 : -i);

                    int count = Frame.Count;
                    int index = FrameIndex;

                    if (CurrentAction == MirAction.DashR || CurrentAction == MirAction.DashL)
                    {
                        count = 3;
                        index %= 3;
                    }

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
                _ when (Poison & PoisonType.DelayedExplosion) == PoisonType.DelayedExplosion => Color.Orange,
                _ when (Poison & (PoisonType.Paralysis | PoisonType.LRParalysis)) != 0 => Color.Gray,
                _ when (Poison & PoisonType.Frozen) == PoisonType.Frozen => Color.Blue,
                _ when (Poison & PoisonType.Blindness) == PoisonType.Blindness => Color.MediumVioletRed,
                _ when (Poison & (PoisonType.Stun | PoisonType.Dazed)) != 0 => Color.Yellow,
                _ when (Poison & PoisonType.Slow) == PoisonType.Slow => Color.Purple,
                _ when (Poison & PoisonType.Bleeding) == PoisonType.Bleeding => Color.DarkRed,
                _ when (Poison & PoisonType.Red) == PoisonType.Red => Color.Red,
                _ when (Poison & PoisonType.Green) == PoisonType.Green => Color.Green,
                _ => Color.White
            };

            if (newColour != DrawColour)
            {
                DrawColour = newColour;
                GameScene.Scene.MapControl.TextureValid = false;
            }
        }
        public virtual void SetAction()
        {
            if (NextAction != null && !GameScene.CanMove)
            {
                switch (NextAction.Action)
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
                        return;
                }
            }

            if (User == this && !GameScene.Observing && CMain.Time < MapControl.NextAction)
                return;

            if (ActionFeed.Count == 0)
            {
                CurrentAction = MirAction.Standing;

                CurrentAction = CMain.Time > BlizzardStopTime ? CurrentAction : MirAction.Stance2;

                if (RidingMount)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Standing:
                            CurrentAction = MirAction.MountStanding;
                            break;
                        case MirAction.Walking:
                            CurrentAction = MirAction.MountWalking;
                            break;
                        case MirAction.Running:
                            CurrentAction = MirAction.MountRunning;
                            break;
                        case MirAction.Struck:
                            CurrentAction = MirAction.MountStruck;
                            break;
                        case MirAction.Attack1:
                            CurrentAction = MirAction.MountAttack;
                            break;
                    }
                }

                if (CurrentAction == MirAction.Standing)
                {
                    if (Class == MirClass.Archer && HasClassWeapon)
                        CurrentAction = MirAction.Standing;
                    else
                        CurrentAction = CMain.Time > StanceTime ? MirAction.Standing : MirAction.Stance;
                }

                if (Fishing) CurrentAction = MirAction.FishingWait;

                Frames.TryGetValue(CurrentAction, out Frame);
                FrameIndex = 0;
                EffectFrameIndex = 0;

                if (MapLocation != CurrentLocation)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = CurrentLocation;
                    GameScene.Scene.MapControl.AddObject(this);
                }

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                // 跟随原始传奇：用该身体模型该动作的 intval（逐模型逐动作每帧毫秒，站250/走90/攻100…）
                // 覆盖帧间隔；无配置（非 origin 玩家/缺失动作）时回落 Frame.Interval。攻击/施法的全局速度倍率仍在其后叠加。
                if (_originModel > 0 && Mir2OriginObjectLibrary.TryGetInterval(_originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), out int oiv) && oiv > 0)
                    FrameInterval = oiv;
                EffectFrameInterval = Frame.EffectInterval;

                SetLibraries();
            }
            else
            {
                QueuedAction action = ActionFeed[0];
                ActionFeed.RemoveAt(0);

                CurrentAction = action.Action;

                if (RidingMount)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Standing:
                            CurrentAction = MirAction.MountStanding;
                            break;
                        case MirAction.Walking:
                            CurrentAction = MirAction.MountWalking;
                            break;
                        case MirAction.Running:
                            CurrentAction = MirAction.MountRunning;
                            break;
                        case MirAction.Struck:
                            CurrentAction = MirAction.MountStruck;
                            break;
                        case MirAction.Attack1:
                            CurrentAction = MirAction.MountAttack;
                            break;
                    }
                }

                CurrentLocation = action.Location;
                MirDirection olddirection = Direction;
                Direction = action.Direction;

                Point temp;
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
                        var steps = 0;
                        if (CurrentAction == MirAction.MountRunning) steps = 3;
                        else if (CurrentAction == MirAction.Running) steps = (Sprint && !Sneaking ? 3 : 2);
                        else steps = 1;

                        temp = Functions.PointMove(CurrentLocation, Direction, CurrentAction == MirAction.Pushed ? 0 : -steps);

                        break;
                    case MirAction.Jump:
                    case MirAction.DashAttack:
                        temp = Functions.PointMove(CurrentLocation, Direction, JumpDistance);
                        break;
                    default:
                        temp = CurrentLocation;
                        break;
                }

                if (MapLocation != temp)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = temp;
                    GameScene.Scene.MapControl.AddObject(this);
                }


                bool ArcherLayTrap = false;

                switch (CurrentAction)
                {
                    case MirAction.Pushed:
                        if (this == User)
                            MapControl.InputDelay = CMain.Time + 500;
                        Frames.TryGetValue(MirAction.Walking, out Frame);
                        break;
                    case MirAction.DashL:
                    case MirAction.DashR:
                        Frames.TryGetValue(MirAction.Running, out Frame);
                        break;
                    case MirAction.DashAttack:
                        Frames.TryGetValue(MirAction.DashAttack, out Frame);
                        break;
                    case MirAction.DashFail:
                        Frames.TryGetValue(RidingMount ? MirAction.MountStanding : MirAction.Standing, out Frame);
                        break;
                    case MirAction.Jump:
                        Frames.TryGetValue(MirAction.Jump, out Frame);
                        break;
                    case MirAction.Attack1:
                        switch (Class)
                        {
                            case MirClass.Archer:
                                Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                            case MirClass.Assassin:
                                if(GameScene.User.DoubleSlash)
                                    Frames.TryGetValue(MirAction.Attack1, out Frame);
                                else if (CMain.Shift)
                                    Frames.TryGetValue(CMain.Random.Next(100) >= 20 ? (CMain.Random.Next(100) > 40 ? MirAction.Attack1 : MirAction.Attack4) : (CMain.Random.Next(100) > 10 ? MirAction.Attack2 : MirAction.Attack3), out Frame);
                                else
                                    Frames.TryGetValue(CMain.Random.Next(100) >= 40 ? MirAction.Attack1 : MirAction.Attack4, out Frame);
                                break;
                            default:
                                if (CMain.Shift && TargetObject == null)
                                    Frames.TryGetValue(CMain.Random.Next(100) >= 20 ? MirAction.Attack1 : MirAction.Attack3, out Frame);
                                else
                                    Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                        }
                        break;
                    case MirAction.Attack4:
                        Spell = (Spell)action.Params[0];
                        Frames.TryGetValue(Spell == Spell.TwinDrakeBlade || Spell == Spell.FlamingSword ? MirAction.Attack1 : CurrentAction, out Frame);
                        break;
                    case MirAction.Spell:
                        Spell = (Spell)action.Params[0];
                        switch (Spell)
                        {
                            case Spell.ShoulderDash:
                                Frames.TryGetValue(MirAction.Running, out Frame);
                                CurrentAction = MirAction.DashL;
                                Direction = olddirection;
                                CurrentLocation = Functions.PointMove(CurrentLocation, Direction, 1);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 2500; //Spell Delay

                                    Network.Enqueue(new C.Magic { ObjectID = GameScene.User.ObjectID, Spell = Spell, Direction = Direction, });
                                }
                                break;
                            case Spell.BladeAvalanche:
                                Frames.TryGetValue(MirAction.Attack3, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                    MapControl.InputDelay = CMain.Time + 1000;
                                }
                                break;
                            case Spell.SlashingBurst:
                                 Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2000; // 80%
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.CounterAttack:
                                Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    GameScene.AttackTime = CMain.Time + User.AttackSpeed;
                                    MapControl.NextAction = CMain.Time + 100; // 80%
                                    GameScene.SpellTime = CMain.Time + 100; //Spell Delay
                                }
                                break;
                            case Spell.PoisonSword:
                                Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2000; // 80%
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.HeavenlySword:
                                Frames.TryGetValue(MirAction.Attack2, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1200;
                                    GameScene.SpellTime = CMain.Time + 1200; //Spell Delay
                                }
                                break;
                            case Spell.CrescentSlash:
                                Frames.TryGetValue(MirAction.Attack3, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.FlashDash:
                                {
                                    int sLevel = (byte)action.Params[3];

                                    GetFlashDashDistance(sLevel);

                                    if (JumpDistance != 0)
                                    {
                                        Frames.TryGetValue(MirAction.DashAttack, out Frame);
                                        CurrentAction = MirAction.DashAttack;
                                        CurrentLocation = Functions.PointMove(CurrentLocation, Direction, JumpDistance);
                                    }
                                    else
                                    {
                                        Frames.TryGetValue(CMain.Random.Next(100) >= 40 ? MirAction.Attack1 : MirAction.Attack4, out Frame);
                                    }

                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time;
                                        GameScene.SpellTime = CMain.Time + 250; //Spell Delay
                                        if (JumpDistance != 0) Network.Enqueue(new C.Magic { Spell = Spell, Direction = Direction });
                                    }
                                }
                                break;
                            case Spell.StraightShot:
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.DoubleShot:                          
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 500; //Spell Delay
                                }
                                break;
                            case Spell.ExplosiveTrap:
                                Frames.TryGetValue(MirAction.Harvest, out Frame);
                                CurrentAction = MirAction.Harvest;
                                ArcherLayTrap = true;
                                if (this == User)
                                {
                                    uint targetID = (uint)action.Params[1];
                                    Point location = (Point)action.Params[2];
                                    Network.Enqueue(new C.Magic { ObjectID = GameScene.User.ObjectID, Spell = Spell, Direction = Direction, TargetID = targetID, Location = location });
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.DelayedExplosion:
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.BackStep:
                                {
                                    int sLevel = (byte)action.Params[3];
                                    GetBackStepDistance(sLevel);
                                    Frames.TryGetValue(MirAction.Jump, out Frame);
                                    CurrentAction = MirAction.Jump;
                                    CurrentLocation = Functions.PointMove(CurrentLocation, Functions.ReverseDirection(Direction), JumpDistance);
                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time + 800;
                                        GameScene.SpellTime = CMain.Time + 2500; //Spell Delay
                                        Network.Enqueue(new C.Magic { ObjectID = GameScene.User.ObjectID, Spell = Spell, Direction = Direction });
                                    }
                                    break;
                                }
                            case Spell.ElementalShot:
                                if (HasElements && !ElementCasted)
                                {
                                    Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                    CurrentAction = MirAction.AttackRange2;
                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time + 1000;
                                        GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                    }
                                }
                                else Frames.TryGetValue(CurrentAction, out Frame);
                                if (ElementCasted) ElementCasted = false;
                                break;
                            case Spell.BindingShot:
                            case Spell.VampireShot:
                            case Spell.PoisonShot:
                            case Spell.CrippleShot:
                            case Spell.NapalmShot:
                            case Spell.SummonVampire:
                            case Spell.SummonToad:
                            case Spell.SummonSnakes:
                            case Spell.Stonetrap:
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1000; //Spell Delay
                                }
                                break;
                            default:
                                Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                        }
                        
                        break;
                    default:
                        Frames.TryGetValue(CurrentAction, out Frame);
                        break;

                }

                //ArcherTest - Need to check for bow weapon only
                if (Class == MirClass.Archer && HasClassWeapon)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Walking:
                            Frames.TryGetValue(MirAction.WalkingBow, out Frame);
                            break;
                        case MirAction.Running:
                            Frames.TryGetValue(MirAction.RunningBow, out Frame);
                            break;
                    }
                }

                //Assassin sneekyness
                if (Class == MirClass.Assassin && Sneaking && (CurrentAction == MirAction.Walking || CurrentAction == MirAction.Running))
                {
                    Frames.TryGetValue(MirAction.Sneek, out Frame);
                }

                SetLibraries();

                FrameIndex = 0;
                EffectFrameIndex = 0;
                Spell = Spell.None;
                SpellLevel = 0;
                //NextMagic = null;

                ClientMagic magic;

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                // 跟随原始传奇：用该身体模型该动作的 intval（逐模型逐动作每帧毫秒，站250/走90/攻100…）
                // 覆盖帧间隔；无配置（非 origin 玩家/缺失动作）时回落 Frame.Interval。攻击/施法的全局速度倍率仍在其后叠加。
                if (_originModel > 0 && Mir2OriginObjectLibrary.TryGetInterval(_originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), out int oiv) && oiv > 0)
                    FrameInterval = oiv;
                EffectFrameInterval = Frame.EffectInterval;

                if (this == User)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.DashFail:
                            //CanSetAction = false;
                            break;
                        case MirAction.Standing:
                        case MirAction.MountStanding:
                            Network.Enqueue(new C.Turn { Direction = Direction });
                            MapControl.NextAction = CMain.Time + 2500;
                            GameScene.CanRun = false;
                            break;
                        case MirAction.Walking:
                        case MirAction.MountWalking:
                        case MirAction.Sneek:
                            GameScene.LastRunTime = CMain.Time;
                            Network.Enqueue(new C.Walk { Direction = Direction });
                            GameScene.Scene.MapControl.FloorValid = false;
                            GameScene.CanRun = true;
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Running:
                        case MirAction.MountRunning:
                            GameScene.LastRunTime = CMain.Time;
                            Network.Enqueue(new C.Run { Direction = Direction });
                            GameScene.Scene.MapControl.FloorValid = false;
                            MapControl.NextAction = CMain.Time + (Sprint ? 1000 : 2500);
                            break;
                        case MirAction.Pushed:
                            GameScene.LastRunTime = CMain.Time;
                            GameScene.Scene.MapControl.FloorValid = false;
                            MapControl.InputDelay = CMain.Time + 500;
                            GameScene.CanRun = false;
                            GameScene.CanMove = false;
                            break;
                        case MirAction.DashL:
                        case MirAction.DashR:
                        case MirAction.Jump:
                        case MirAction.DashAttack:
                            GameScene.LastRunTime = CMain.Time;
                            GameScene.Scene.MapControl.FloorValid = false;
                            GameScene.CanRun = false;
                            //CanSetAction = false;
                            break;
                        case MirAction.Mine:
                            Network.Enqueue(new C.Attack { Direction = Direction, Spell = Spell.None });
                            GameScene.AttackTime = CMain.Time + (1400 - Math.Min(370, (User.Level * 14)));
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Attack1:
                        case MirAction.MountAttack:

                            if (!RidingMount)
                            {
                                if (GameScene.User.Slaying && (TargetObject != null || GameScene.Observing))
                                    Spell = Spell.Slaying;

                                if (GameScene.User.Thrusting && GameScene.Scene.MapControl.HasTarget(Functions.PointMove(CurrentLocation, Direction, 2)))
                                    Spell = Spell.Thrusting;

                                if (GameScene.User.HalfMoon)
                                {
                                    if (TargetObject != null || GameScene.Scene.MapControl.CanHalfMoon(CurrentLocation, Direction))
                                    {
                                        magic = User.GetMagic(Spell.HalfMoon);
                                        if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                            Spell = Spell.HalfMoon;
                                    }
                                }

                                if (GameScene.User.CrossHalfMoon)
                                {
                                    if (TargetObject != null || GameScene.Scene.MapControl.CanCrossHalfMoon(CurrentLocation))
                                    {
                                        magic = User.GetMagic(Spell.CrossHalfMoon);
                                        if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                            Spell = Spell.CrossHalfMoon;
                                    }
                                }

                                if (GameScene.User.DoubleSlash)
                                {
                                    magic = User.GetMagic(Spell.DoubleSlash);
                                    if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                        Spell = Spell.DoubleSlash;
                                }


                                if (GameScene.User.TwinDrakeBlade && (TargetObject != null || GameScene.Observing))
                                {
                                    magic = User.GetMagic(Spell.TwinDrakeBlade);
                                    if (magic != null && magic.BaseCost + magic.LevelCost * magic.Level <= User.MP)
                                        Spell = Spell.TwinDrakeBlade;
                                }

                                if (GameScene.User.FlamingSword)
                                {
                                    if (TargetObject != null)
                                    {
                                        magic = User.GetMagic(Spell.FlamingSword);
                                        if (magic != null)
                                        {
                                            Spell = Spell.FlamingSword;
                                            magic.CastTime = CMain.Time;
                                        }
                                    }
                                }
                            }

                            Network.Enqueue(new C.Attack { Direction = Direction, Spell = Spell });

                            if (Spell == Spell.Slaying)
                                GameScene.User.Slaying = false;
                            if (Spell == Spell.TwinDrakeBlade)
                                GameScene.User.TwinDrakeBlade = false;
                            if (Spell == Spell.FlamingSword)
                                GameScene.User.FlamingSword = false;

                            magic = User.GetMagic(Spell);

                            if (magic != null) SpellLevel = magic.Level;

                            GameScene.AttackTime = CMain.Time + User.AttackSpeed;
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Attack2:
                            //Network.Enqueue(new C.Attack2 { Direction = Direction });
                            break;
                        case MirAction.Attack3:
                            //Network.Enqueue(new C.Attack3 { Direction = Direction });
                            break;
                        //case MirAction.Attack4:
                        //    GameScene.AttackTime = CMain.Time;// + User.AttackSpeed;
                        //    MapControl.NextAction = CMain.Time;
                        //    break;

                        case MirAction.AttackRange1:
                            {
                                GameScene.AttackTime = CMain.Time + User.AttackSpeed + 200;

                                uint targetID = (uint)action.Params[0];
                                Point location = (Point)action.Params[1];

                                Network.Enqueue(new C.RangeAttack { Direction = Direction, Location = CurrentLocation, TargetID = targetID, TargetLocation = location });
                            }
                            break;
                        case MirAction.AttackRange2:
                        case MirAction.Spell:
                            {
                                Spell = (Spell)action.Params[0];
                                uint targetID = (uint)action.Params[1];
                                Point location = (Point)action.Params[2];

                                Network.Enqueue(new C.Magic { ObjectID = GameScene.User.ObjectID, Spell = Spell, Direction = Direction, TargetID = targetID, Location = location });

                                if (Spell == Spell.FlashDash)
                                {
                                    GameScene.SpellTime = CMain.Time + 250;
                                    MapControl.NextAction = CMain.Time;
                                }
                                else
                                {
                                    GameScene.SpellTime = Spell == Spell.FlameField ? CMain.Time + 2500 : CMain.Time + 1800;
                                    MapControl.NextAction = CMain.Time + 2500;
                                }
                            }
                            break;                         
                        case MirAction.Harvest:
                            if (ArcherLayTrap)
                            {
                                ArcherLayTrap = false;
                                SoundManager.PlaySound(20000 + 124 * 10);
                            }
                            else
                            {
                                Network.Enqueue(new C.Harvest { Direction = Direction });
                                MapControl.NextAction = CMain.Time + 2500;
                            }
                            break;

                    }
                }


                switch (CurrentAction)
                {
                    case MirAction.Pushed:
                        FrameIndex = Frame.Count - 1;
                        EffectFrameIndex = Frame.EffectCount - 1;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashL:
                    case MirAction.Jump:
                        FrameIndex = 0;
                        EffectFrameIndex = 0;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashR:
                        FrameIndex = 3;
                        EffectFrameIndex = 3;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.Walking:
                    case MirAction.Running:
                    case MirAction.MountWalking:
                    case MirAction.MountRunning:
                    case MirAction.Sneek:
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashAttack:
                        //FrameIndex = 0;
                        //EffectFrameIndex = 0;
                        GameScene.Scene.Redraw();

                        if (IsDashAttack())
                        {
                            action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                            action.Params.Add(Spell.FlashDash);
                            ActionFeed.Insert(0, action);
                        }
                        break;
                    case MirAction.Attack1:
                        if (this != User)
                        {
                            Spell = (Spell)action.Params[0];
                            SpellLevel = (byte)action.Params[1];
                        }

                        switch (Spell)
                        {
                            case Spell.Slaying:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                break;
                            case Spell.DoubleSlash:
                                FrameInterval = (int)(FrameInterval * 0.46f); //46% Animation Speed
                                EffectFrameInterval = (int)(EffectFrameInterval * 0.46f);

                                action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                                action.Params.Add(Spell);
                                ActionFeed.Insert(0, action);

                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            case Spell.Thrusting:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            case Spell.HalfMoon:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.TwinDrakeBlade:
                                //FrameInterval = FrameInterval * 9 / 10; //70% Faster Animation
                                //EffectFrameInterval = EffectFrameInterval * 9 / 10;
                                //action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                                //action.Params.Add(Spell);
                                //ActionFeed.Insert(0, action);
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.CrossHalfMoon:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.FlamingSword:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                break;

                            
                        }
                        break;
                    case MirAction.Attack4:
                        Spell = (Spell)action.Params[0];
                        switch (Spell)
                        {
                            case Spell.DoubleSlash:
                                FrameInterval = (int)(FrameInterval * 0.46f); //46% Animation Speed
                                EffectFrameInterval = (int)(EffectFrameInterval * 0.46f);

                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                break;
                            case Spell.TwinDrakeBlade:
                                FrameInterval = FrameInterval * 9 / 10; //80% Animation Speed
                                EffectFrameInterval = EffectFrameInterval * 9 / 10;
                                break;
                            case Spell.FlashDash:
                                int attackDelay = (User.AttackSpeed - 120) <= 300 ? 300 : (User.AttackSpeed - 120);

                                float attackRate = (float)(attackDelay / 300F * 10F);
                                FrameInterval = FrameInterval * (int)attackRate / 20;
                                EffectFrameInterval = EffectFrameInterval * (int)attackRate / 20;
                                break;
                        }
                        break;
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        uint attackerID = (uint)action.Params[0];
                        StruckWeapon = -2;

                        if (action.Params.Count > 1)
                        {
                            StruckWeapon = (int)action.Params[1];
                        }
                        else if (MapControl.Objects.TryGetValue(attackerID, out MapObject ob))
                            if (ob.Race == ObjectType.Player)
                            {
                                PlayerObject player = (PlayerObject)ob;
                                StruckWeapon = player.Weapon;
                                if (player.Class == MirClass.Assassin && StruckWeapon != -1)
                                    StruckWeapon = 1;
                            }

                        PlayStruckSound();
                        PlayFlinchSound();
                        break;
                    case MirAction.AttackRange1: //ArcherTest - Assign Target for other users
                        if (this != User)
                        {
                            TargetID = (uint)action.Params[0];
                            TargetPoint = (Point)action.Params[1];
                            Spell = (Spell)action.Params[2];
                        }
                        break;
                    case MirAction.AttackRange2:
                    case MirAction.Spell:
                        if (this != User)
                        {
                            Spell = (Spell)action.Params[0];
                            TargetID = (uint)action.Params[1];
                            TargetPoint = (Point)action.Params[2];
                            Cast = (bool)action.Params[3];
                            SpellLevel = (byte)action.Params[4];
                            SecondaryTargetIDs = (List<uint>)action.Params[5];
                        }

                        switch (Spell)
                        {

                            case Spell.FireBall:
                                Effects.Add(new Effect(Libraries.Magic, 0, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Healing:
                                Effects.Add(new Effect(Libraries.Magic, 200, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Repulsion:
                                Effects.Add(new Effect(Libraries.Magic, 900, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.ElectricShock:
                                Effects.Add(new Effect(Libraries.Magic, 1560, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Poisoning:
                                Effects.Add(new Effect(Libraries.Magic, 600, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.GreatFireBall:
                                Effects.Add(new Effect(Libraries.Magic, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.HellFire:
                                Effects.Add(new Effect(Libraries.Magic, 920, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.ThunderBolt:
                                Effects.Add(new Effect(Libraries.Magic2, 20, 3, 300, this));
                                break;



                            case Spell.SoulFireBall:
                                // 起手帧静默：原始传奇 5config.skillEffects[1304101] 只有 type=2 子弹带 sound=20025，
                                // 起手/命中帧都无独立音效。该音效统一在下方符纸飞出帧播放，避免同一技能响两遍。
                                break;



                            case Spell.SummonSkeleton:
                                // 起手特效改走原始传奇 CDN（5config：召唤骷髅 skill 13051 → effect 655/model13005），
                                // 替代已移除的经典 .Lib（Libraries.Magic/1500）。
                                Effects.Add(new OriginEffect(655, this));
                                // 原始传奇音效：5config.skillEffects[1305101].sound=200171
                                SoundManager.PlayOriginSound(200171);
                                break;

                            case Spell.StormEscape:
                                Effects.Add(new Effect(Libraries.Magic3, 590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.Teleport:
                                Effects.Add(new Effect(Libraries.Magic, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Blink:
                                Effects.Add(new Effect(Libraries.Magic, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Hiding:
                                Effects.Add(new Effect(Libraries.Magic, 1520, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Haste:
                                Effects.Add(new Effect(Libraries.Magic2, 2140 + (int)Direction * 10, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Fury:
                                Effects.Add(new Effect(Libraries.Magic3, 200, 8, 8 * FrameInterval, this));
                                Effects.Add(new Effect(Libraries.Magic3, 187, 10, 10 * FrameInterval, this));
                                //i don't know sound
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;


                            case Spell.ImmortalSkin:
                                Effects.Add(new Effect(Libraries.Magic3, 550, 17, Frame.Count * FrameInterval * 4, this));
                                Effects.Add(new Effect(Libraries.Magic3, 570, 5, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;


                            case Spell.FireBang:
                                Effects.Add(new Effect(Libraries.Magic, 1650, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.FireWall:
                                Effects.Add(new Effect(Libraries.Magic, 1620, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.HealingCircle:
                                Effects.Add(new Effect(Libraries.Magic3, 620, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.MoonMist:
                                MapControl.Effects.Add(new Effect(Libraries.Magic3, 680, 25, 1800, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.TrapHexagon:
                                Effects.Add(new Effect(Libraries.Magic, 1380, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.EnergyRepulsor:
                                Effects.Add(new Effect(Libraries.Magic2, 190, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.FireBurst:
                                Effects.Add(new Effect(Libraries.Magic2, 2320, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.FlameDisruptor:
                                Effects.Add(new Effect(Libraries.Magic2, 130, 6, Frame.Count * FrameInterval, this));
                                break;



                            case Spell.SummonShinsu:
                                // 起手特效改走原始传奇 CDN（5config：召唤神兽 skill 13131 → Lv1 effect652/model13002、Lv5 759、Lv6 765、Lv7 771），
                                // 替代已移除的经典 .Lib（Libraries.Magic2/0）。
                                {
                                    int shinsuEffect = SpellLevel >= 7 ? 771 : SpellLevel >= 6 ? 765 : SpellLevel >= 5 ? 759 : 652;
                                    Effects.Add(new OriginEffect(shinsuEffect, this));
                                }
                                // 原始传奇音效：5config.skillEffects[1313101..1313107].sound=20031（全等级同款）
                                SoundManager.PlayOriginSound(20031);
                                break;



                            case Spell.UltimateEnhancer:
                                Effects.Add(new Effect(Libraries.Magic2, 160, 15, 1000, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.FrostCrunch:
                                Effects.Add(new Effect(Libraries.Magic2, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Purification:
                                Effects.Add(new Effect(Libraries.Magic2, 600, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.FlameField:
                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 910, 23, 1800, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Trap:
                                Effects.Add(new Effect(Libraries.Magic2, 2340, 11, 11 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.MoonLight:
                                Effects.Add(new Effect(Libraries.Magic2, 2380, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.SwiftFeet:
                                Effects.Add(new Effect(Libraries.Magic2, 2440, 16, 16 * EffectFrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.LightBody:
                                Effects.Add(new Effect(Libraries.Magic2, 2470, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;




                            case Spell.PoisonSword:
                                Effects.Add(new Effect(Libraries.Magic2, 2490 + ((int)Direction * 10), 10, Frame.Count * FrameInterval + 500, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.DarkBody:
                                Effects.Add(new Effect(Libraries.Magic2, 2580, 10, 10 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.ThunderStorm:
                                MapControl.Effects.Add(new Effect(Libraries.Magic, 1680, 10, Frame.Count * FrameInterval, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.MassHealing:
                                Effects.Add(new Effect(Libraries.Magic, 1790, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.IceStorm:
                                Effects.Add(new Effect(Libraries.Magic, 3840, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.MagicShield:
                                Effects.Add(new Effect(Libraries.Magic, 3880, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.TurnUndead:
                                Effects.Add(new Effect(Libraries.Magic, 3920, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.MagicBooster:
                                Effects.Add(new Effect(Libraries.Magic3, 80, 9, 9 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.PetEnhancer:
                                Effects.Add(new Effect(Libraries.Magic3, 200, 8, 8 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Revelation:
                                Effects.Add(new Effect(Libraries.Magic, 3960, 20, 1200, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.ProtectionField:
                                Effects.Add(new Effect(Libraries.Magic2, 1520, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Rage:
                                Effects.Add(new Effect(Libraries.Magic2, 1510, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;




                            case Spell.Vampirism:
                                Effects.Add(new Effect(Libraries.Magic2, 1040, 7, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.LionRoar:
                            case Spell.BattleCry:
                                Effects.Add(new Effect(Libraries.Magic2, 710, 20, 1200, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                break;



                            case Spell.TwinDrakeBlade:
                                Effects.Add(new Effect(Libraries.Magic2, 210, 6, 500, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Entrapment:
                                Effects.Add(new Effect(Libraries.Magic2, 990, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.BladeAvalanche:
                                Effects.Add(new Effect(Libraries.Magic2, 740 + (int)Direction * 20, 15, 15 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.SlashingBurst:
                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 1700 + (int)Direction * 10, 9, 9 * FrameInterval, CurrentLocation, CMain.Time + 300));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                SlashingBurstTime = CMain.Time + 2000;
                                break;



                            case Spell.CounterAttack:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 5);
                                Effects.Add(new Effect(Libraries.Magic, 3480 + (int)Direction * 10, 10, 10 * FrameInterval, this));
                                Effects.Add(new Effect(Libraries.Magic3, 140, 2, 2 * FrameInterval, this));
                                break;



                            case Spell.CrescentSlash:
                                Effects.Add(new Effect(Libraries.Magic2, 2620 + (int)Direction * 20, 20, 20 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));

                               
                                break;



                            case Spell.FlashDash:
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                int attackDelay = (User.AttackSpeed - 120) <= 300 ? 300 : (User.AttackSpeed - 120);

                                float attackRate = (float)(attackDelay / 300F * 10F);
                                FrameInterval = FrameInterval * (int)attackRate / 20;
                                EffectFrameInterval = EffectFrameInterval * (int)attackRate / 20;
                                break;


                            case Spell.Mirroring:
                                Effects.Add(new Effect(Libraries.Magic2, 650, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.Blizzard:
                                Effects.Add(new Effect(Libraries.Magic2, 1540, 8, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                BlizzardStopTime = CMain.Time + 3000;
                                break;



                            case Spell.MeteorStrike:
                                Effects.Add(new Effect(Libraries.Magic2, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                BlizzardStopTime = CMain.Time + 3000;
                                break;



                            case Spell.Reincarnation:
                                ReincarnationStopTime = CMain.Time + 6000;
                                break;



                            case Spell.HeavenlySword:
                                Effects.Add(new Effect(Libraries.Magic2, 2230 + ((int)Direction * 10), 8, 800, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.ElementalBarrier:
                                if (HasElements && !ElementalBarrier)
                                {
                                    Effects.Add(new Effect(Libraries.Magic3, 1880, 8, Frame.Count * FrameInterval, this));
                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                }
                                break;


                            case Spell.PoisonShot:
                                Effects.Add(new Effect(Libraries.Magic3, 2300, 8, 1000, this));
                                break;

                            case Spell.OneWithNature:
                                MapControl.Effects.Add(new Effect(Libraries.Magic3, 2710, 8, 1200, CurrentLocation));
                                SoundManager.PlaySound(20000 + 139 * 10);
                                break;



                            case Spell.FireBounce:
                                Effects.Add(new Effect(Libraries.Magic, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10);
                                break;



                            case Spell.MeteorShower:
                                Effects.Add(new Effect(Libraries.Magic, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10);
                                break;


                        }


                        break;
                    case MirAction.Dead:
                        GameScene.Scene.Redraw();
                        GameScene.Scene.MapControl.SortObject(this);
                        if (MouseObject == this) MouseObjectID = 0;
                        if (SelectionTarget == this || TargetObject == this || MagicObject == this)
                            SelectTarget(null);
                        DeadTime = CMain.Time;
                        break;

                }

            }

            GameScene.Scene.MapControl.TextureValid = false;

            NextMotion = CMain.Time + FrameInterval;
            NextMotion2 = CMain.Time + EffectFrameInterval;

            if (ElementalBarrier)
            {
                switch (CurrentAction)
                {
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        if (ElementalBarrierEffect != null)
                        {
                            ElementalBarrierEffect.Clear();
                            ElementalBarrierEffect.Remove();
                        }

                        Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1910, 5, 600, this));
                        ElementalBarrierEffect.Complete += (o, e) => Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                        break;
                    default:
                        if (ElementalBarrierEffect == null)
                            Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                        break;
                }
            }

            if (MagicShield)
            {
                switch (CurrentAction)
                {
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        if (ShieldEffect != null)
                        {
                            ShieldEffect.Clear();
                            ShieldEffect.Remove();
                        }

                        Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3900, 3, 600, this));
                        ShieldEffect.Complete += (o, e) => Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                        break;
                    default:
                        if (ShieldEffect == null)
                            Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                        break;
                }
            }

        }

        public virtual void ProcessFrames()
        {
            if (Frame == null) return;

            switch (CurrentAction)
            {
                case MirAction.Walking:
                case MirAction.Running:
                case MirAction.MountWalking:
                case MirAction.MountRunning:
                case MirAction.Sneek:
                case MirAction.DashAttack:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    if (SkipFrames) FrameIndex = Frame.Count;

                    if (UpdateFrame(false) >= Frame.Count)
                    {
                        FrameIndex = Frame.Count - 1;
                        SetAction();
                    }
                    else
                    {
                        if (this == User)
                        {
                            if (FrameIndex == 1 || FrameIndex == 4)
                                PlayStepSound();
                        }
                    }

                    UpdateWingEffect();
                    break;
                 case MirAction.Jump:
                    if (!GameScene.CanMove) return;
                    GameScene.Scene.MapControl.TextureValid = false;
                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    if (SkipFrames) UpdateFrame();
                    if (UpdateFrame() >= Frame.Count)
                    {
                        FrameIndex = Frame.Count - 1;
                        SetAction();
                    }
                    else
                    {
                        if (FrameIndex == 1)
                            SoundManager.PlaySound(20000 + 127 * 10 + (Gender == MirGender.Male ? 5 : 6));
                        if (FrameIndex == 7)
                            SoundManager.PlaySound(20000 + 127 * 10 + 7);
                    }
                    //Backstep wingeffect
                    UpdateWingEffect();
                    break;
                case MirAction.DashL:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    if (UpdateFrame() >= 3)
                    {
                        FrameIndex = 2;
                        SetAction();
                    }

                    if (UpdateFrame2() >= 3) EffectFrameIndex = 2;
                    break;
                case MirAction.DashR:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;

                    if (UpdateFrame() >= 6)
                    {
                        FrameIndex = 5;
                        SetAction();
                    }

                    if (UpdateFrame2() >= 6) EffectFrameIndex = 5;
                    break;
                case MirAction.Pushed:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;

                    FrameIndex -= 2;
                    EffectFrameIndex -= 2;

                    if (FrameIndex < 0)
                    {
                        FrameIndex = 0;
                        SetAction();
                    }

                    if (EffectFrameIndex < 0) EffectFrameIndex = 0;
                    break;

                case MirAction.Standing:
                case MirAction.MountStanding:
                case MirAction.DashFail:
                case MirAction.Harvest:
                case MirAction.Stance:
                case MirAction.Stance2:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;  


                case MirAction.FishingCast:             
                case MirAction.FishingReel:
                case MirAction.FishingWait:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            switch (FrameIndex)
                            {
                                case 1:
                                    switch (CurrentAction)
                                    {
                                        case MirAction.FishingCast:
                                            SoundManager.PlaySound(SoundList.FishingThrow);
                                            ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = false;
                                            break;
                                        case MirAction.FishingReel:
                                            SoundManager.PlaySound(SoundList.FishingPull);
                                            break;
                                        case MirAction.FishingWait:
                                            if (FoundFish)
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 671, 6, 720, FishingPoint) { Light = 0 });
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 665, 6, 720, FishingPoint) { Light = 0 });

                                                SoundManager.PlaySound(SoundList.Fishing);
                                                Effects.Add(new Effect(Libraries.Prguse, 1350, 2, 720, this) { Light = 0, Blend = false });

                                                ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = true;
                                            }
                                            else
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 650, 6, 720, FishingPoint) { Light = 0 });
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 640, 6, 720, FishingPoint) { Light = 0 });
                                                ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = false;
                                            }

                                            ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).AnimationCount = FoundFish ? 10 : 1;
                                            break;
                                    }
                                    break;
                            }
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;     

                case MirAction.Attack1:
                case MirAction.Attack2:
                case MirAction.Attack3:
                case MirAction.Attack4:
                case MirAction.MountAttack:
                case MirAction.Mine:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            //if (ActionFeed.Count == 0)
                            //    ActionFeed.Add(new QueuedAction { Action = MirAction.Stance, Direction = Direction, Location = CurrentLocation });

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1) PlayAttackSound();
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;

                case MirAction.AttackRange1:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1) PlayAttackSound();
                            Missile missile;
                            switch (FrameIndex)
                            {
                                case 6:
                                    switch (Spell)
                                    {
                                        case Spell.Focus:
                                            Effects.Add(new Effect(Libraries.Magic3, 2730, 10, Frame.Count * FrameInterval, this));
                                            SoundManager.PlaySound(20000 + 121 * 10 + 5);
                                            break;
                                    }

                                    break;
                                case 5:
                                    missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);
                                    StanceTime = CMain.Time + StanceDelay;
                                    SoundManager.PlaySound(20000 + 121 * 10);
                                    if (missile.Target != null)
                                    {
                                        missile.Complete += (o, e) =>
                                        {
                                            SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                        };
                                    }
                                    break;
                            }

                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;

                case MirAction.AttackRange2:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            if (Cast)
                            {
                                MapObject ob = MapControl.GetObject(TargetID);

                                Missile missile;
                                switch (Spell)
                                {
                                    case Spell.StraightShot:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 0);
                                        missile = CreateProjectile(1210, Libraries.Magic3, true, 5, 30, 5);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic3, 1370, 7, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.StraightShot * 10 + 2);
                                            };
                                        }
                                        break;
                                }


                                Cast = false;
                            }

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();

                        }
                        else
                        {
                            NextMotion += FrameInterval;

                            Missile missile;

                            switch(Spell)
                            {
                                case Spell.DoubleShot:
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                        case 5:
                                            missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);//normal arrow
                                            StanceTime = CMain.Time + StanceDelay;
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.ElementalShot:
                                    if (HasElements && !ElementCasted)
                                        switch (FrameIndex)
                                        {
                                            case 7:
                                                missile = CreateProjectile(1690, Libraries.Magic3, true, 6, 30, 4);//elemental arrow
                                                StanceTime = CMain.Time + StanceDelay;
                                                if (missile.Target != null)
                                                {
                                                    missile.Complete += (o, e) =>
                                                    {
                                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);//sound M128-2
                                                    };
                                                }
                                                break;
                                            case 1:
                                                Effects.Add(new Effect(Libraries.Magic3, 1681, 5, Frame.Count * FrameInterval, this));
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 0);//sound M128-0
                                                break;
                                        }
                                    break;
                                case Spell.BindingShot:
                                case Spell.SummonVampire:
                                case Spell.SummonToad:
                                case Spell.SummonSnakes:
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            missile = CreateProjectile(2750, Libraries.Magic3, true, 5, 10, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 7);//sound M130-7
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.Stonetrap:
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            missile = CreateProjectile(2750, Libraries.Magic3, true, 5, 20, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                            {
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 7);//sound M130-7
                                            };

                                            break;
                                    }
                                    break;
                                case Spell.DelayedExplosion:
                                    switch (FrameIndex)
                                    {
                                        case 5:
                                            missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);//normal arrow
                                            StanceTime = CMain.Time + StanceDelay;
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.VampireShot:
                                case Spell.PoisonShot:
                                case Spell.CrippleShot:
                                    MapObject ob = MapControl.GetObject(TargetID);
                                    Effect eff;
                                    int exFrameStart = 0;
                                    if (Spell == Spell.PoisonShot) exFrameStart = 200;
                                    if (Spell == Spell.CrippleShot) exFrameStart = 400;
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + ((Spell == Spell.CrippleShot) ? 136 : 121) * 10);//M136-0
                                            missile = CreateProjectile(1930 + exFrameStart, Libraries.Magic3, true, 5, 10, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (ob != null)
                                                    {
                                                        if (Spell == Spell.CrippleShot)
                                                        {
                                                            int exIdx = 0;
                                                            if (this == User)
                                                            {
                                                                if (GameScene.Scene.BuffsDialog.Buffs.Any(x => x.Type == BuffType.VampireShot)) exIdx = 20;
                                                                if (GameScene.Scene.BuffsDialog.Buffs.Any(x => x.Type == BuffType.PoisonShot)) exIdx = 10;
                                                            }
                                                            else
                                                            {
                                                                if (Buffs.Any(x => x == BuffType.VampireShot)) exIdx = 20;
                                                                if (Buffs.Any(x => x == BuffType.PoisonShot)) exIdx = 10;
                                                            }

                                                            ob.Effects.Add(eff = new Effect(Libraries.Magic3, 2490 + exIdx, 7, 1000, ob));
                                                            SoundManager.PlaySound(20000 + 136 * 10 + 5 + (exIdx / 10));//sound M136-5/7

                                                            if (exIdx == 20)
                                                                eff.Complete += (o1, e1) =>
                                                                {
                                                                    SoundManager.PlaySound(20000 + 45 * 10 + 2);//sound M45-2
                                                                    Effects.Add(new Effect(Libraries.Magic3, 2100, 8, 1000, this));
                                                                };
                                                        }

                                                        if (Spell == Spell.VampireShot || Spell == Spell.PoisonShot)
                                                        {
                                                            ob.Effects.Add(eff = new Effect(Libraries.Magic3, 2090 + exFrameStart, 6, 1000, ob));
                                                            SoundManager.PlaySound(20000 + (133 + (exFrameStart / 100)) * 10 + 2);//sound M133-2 or M135-2
                                                            if (Spell == Spell.VampireShot)
                                                                eff.Complete += (o1, e1) =>
                                                                {
                                                                    SoundManager.PlaySound(20000 + 45 * 10 + 2);//sound M45-2
                                                                    Effects.Add(new Effect(Libraries.Magic3, 2100 + exFrameStart, 8, 1000, this));
                                                                };
                                                        }
                                                    }
                                                    //SoundManager.PlaySound(20000 + 121 * 10 + 2);//sound M121-2
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.NapalmShot:
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 138 * 10);//M138-0
                                            missile = CreateProjectile(2530, Libraries.Magic3, true, 6, 50, 4);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 138 * 10 + 2);//M138-2
                                                    MapControl.Effects.Add(new Effect(Libraries.Magic3, 2690, 10, 1000, TargetPoint));
                                                };
                                            }
                                            break;
                                    }
                                    break;
                            }
                        }
                    }

                    UpdateWingEffect();
                    break;

                case MirAction.Struck:
                case MirAction.MountStruck:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;
                case MirAction.Spell:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            if (Cast)
                            {

                                MapObject ob = MapControl.GetObject(TargetID);

                                Missile missile;
                                Effect effect;
                                switch (Spell)
                                {

                                    case Spell.FireBall:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        missile = CreateProjectile(10, Libraries.Magic, true, 6, 30, 4);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic, 170, 10, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                            };
                                        }
                                        break;



                                    case Spell.GreatFireBall:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        missile = CreateProjectile(410, Libraries.Magic, true, 6, 30, 4);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 2);
                                            };
                                        }
                                        break;



                                    case Spell.Healing:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, ob));
                                        break;



                                    case Spell.ElectricShock:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1570, 10, 1000, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 1570, 10, 1000, ob));
                                        break;


                                    case Spell.Poisoning:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob != null)
                                            ob.Effects.Add(new Effect(Libraries.Magic, 770, 10, 1000, ob));
                                        break;


                                    case Spell.HellFire:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);

                                        
                                        Point dest = CurrentLocation;
                                        for (int i = 0; i < 4; i++)
                                        {
                                            dest = Functions.PointMove(dest, Direction, 1);
                                            if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                            effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                            effect.SetStart(CMain.Time + i * 50);
                                            MapControl.Effects.Add(effect);
                                        }

                                        if (SpellLevel == 3)
                                        {
                                            MirDirection dir = (MirDirection)(((int)Direction + 1) % 8);
                                            dest = CurrentLocation;
                                            for (int r = 0; r < 4; r++)
                                            {
                                                dest = Functions.PointMove(dest, dir, 1);
                                                if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                                effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                                effect.SetStart(CMain.Time + r * 50);
                                                MapControl.Effects.Add(effect);
                                            }

                                            dir = (MirDirection)(((int)Direction - 1 + 8) % 8);
                                            dest = CurrentLocation;
                                            for (int r = 0; r < 4; r++)
                                            {
                                                dest = Functions.PointMove(dest, dir, 1);
                                                if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                                effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                                effect.SetStart(CMain.Time + r * 50);
                                                MapControl.Effects.Add(effect);
                                            }
                                        }
                                        break;



                                    case Spell.ThunderBolt:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);

                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 10, 5, 400, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 10, 5, 400, ob));
                                        break;



                                    case Spell.SoulFireBall:
                                        // 符纸飞出瞬间播原始传奇音效：5config.skillEffects[1304101].sound=20025
                                        SoundManager.PlayOriginSound(20025);
                                        // 原始传奇 5config.effect 核对过 CDN 实际画面：
                                        //   653/model13003 = 符纸（多张小符），752/model13024 = Lv5 符纸  → 飞行道具
                                        //   654/model13023 = 火焰爆炸，754/model13026 = Lv5 火焰      → 抵达目标后的爆炸
                                        // 之前把 654/754（火焰）当成飞行符从玩家身上发射，看起来就是“爆炸直接在玩家身上播放”；
                                        // 正确顺序是符纸先飞、命中时才在目标身上炸出火焰。
                                        //
                                        // 权威时序来自 5config.skillEffects[1304101/1304105]（原始传奇 JS 同款）：
                                        //   type=2 子弹 effect653/752 delaytime=400 → 飞行时长 400ms
                                        //   type=1 命中 effect654/754 delaytime=200 → 命中后火焰
                                        //   skill.hurtDelay=500 → 伤害在施法后 500ms 结算
                                        // 起点由 OriginMissile 内部按 GameDefine.SKILL_HitTestY=60 抬到手部。
                                        {
                                            int flyEffect = SpellLevel >= 5 ? 752 : 653;
                                            int hitEffect = SpellLevel >= 5 ? 754 : 654;
                                            const int flyDelay = 400;  // skillEffects type=2 delaytime
                                            Point flyDest = ob != null ? ob.CurrentLocation : TargetPoint;
                                            OriginMissile originMissile = new OriginMissile(flyEffect, this, ob, flyDest,
                                                                                            flightDuration: flyDelay);
                                            MapObject hitTarget = ob;
                                            originMissile.Complete += (o, e) =>
                                            {
                                                if (hitTarget == null || hitTarget.CurrentAction == MirAction.Dead) return;
                                                hitTarget.Effects.Add(new OriginEffect(hitEffect, hitTarget));
                                                // 原始传奇命中特效(effect654/754)配置无独立 sound，命中不再叠加经典音效。
                                            };
                                            Effects.Add(originMissile);
                                        }
                                        break;



                                    case Spell.EnergyShield:

                                        //Effects.Add(new Effect(Libraries.Magic2, 1880, 9, Frame.Count * FrameInterval, this));
                                        //SoundManager.PlaySound(20000 + (ushort)Spell * 9);
                                        break;



                                    case Spell.FireBang:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic, 1660, 10, 1000, TargetPoint));
                                        break;



                                    case Spell.MassHiding:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1540, 10, 800, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.MassHiding * 10 + 1);
                                        };
                                        break;



                                    case Spell.SoulShield:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1320, 15, 1200, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.SoulShield * 10 + 1);
                                        };
                                        break;



                                    case Spell.BlessedArmour:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1340, 15, 1200, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.BlessedArmour * 10 + 1);
                                        };
                                        break;



                                    case Spell.FireWall:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        break;




                                    case Spell.HealingCircle:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic3, 620, 10, 1200, TargetPoint));
                                        break;



                                    case Spell.MassHealing:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic, 1800, 10, 1000, TargetPoint));
                                        break;



                                    case Spell.IceStorm:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic, 3850, 20, 1300, TargetPoint));
                                        break;



                                    case Spell.TurnUndead:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 3930, 15, 1000, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 3930, 15, 1000, ob));
                                        break;


                                    case Spell.IceThrust:

                                        Point location = Functions.PointMove(CurrentLocation, Direction, 1);

                                        MapControl.Effects.Add(new Effect(Libraries.Magic2, 1790 + (int)Direction * 10, 10, 10 * FrameInterval, location));
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        break;



                                    case Spell.Revelation:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 3990, 10, 1000, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic, 3990, 10, 1000, ob));
                                        break;


                                    case Spell.FlameDisruptor:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);

                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 140, 10, 600, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 140, 10, 600, ob));
                                        break;


                                    case Spell.CatTongue:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        missile = CreateProjectile(260, Libraries.Magic3, true, 6, 30, 4);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic3, 420, 8, 800, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.CatTongue * 10 + 1);
                                            };
                                        }
                                        break;



                                    case Spell.FrostCrunch:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        missile = CreateProjectile(410, Libraries.Magic2, true, 4, 30, 6);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic2, 570, 8, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.FrostCrunch * 10 + 2);
                                            };
                                        }
                                        break;



                                    case Spell.Purification:
                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 620, 10, 800, TargetPoint));
                                        else
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 620, 10, 800, ob));
                                        break;



                                    case Spell.Curse:
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 950, 24, 2000, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.Curse * 10);
                                        };
                                        break;



                                    case Spell.Hallucination:
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 48, 7);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic2, 1110, 10, 1000, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            };
                                        }
                                        break;



                                    case Spell.Lightning:
                                        Effects.Add(new Effect(Libraries.Magic, 970 + (int)Direction * 20, 6, Frame.Count * FrameInterval, this));
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        break;



                                    case Spell.Vampirism:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);

                                        if (ob == null)
                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 1060, 20, 1000, TargetPoint));
                                        else
                                        {
                                            ob.Effects.Add(effect = new Effect(Libraries.Magic2, 1060, 20, 1000, ob));
                                            effect.Complete += (o, e) =>
                                            {
                                                SoundManager.PlaySound(20000 + (ushort)Spell.Vampirism * 10 + 2);
                                                Effects.Add(new Effect(Libraries.Magic2, 1090, 10, 500, this));
                                            };
                                        }
                                        break;



                                    case Spell.PoisonCloud:
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                            {
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            };

                                        break;



                                    case Spell.Blizzard:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        //BlizzardFreezeTime = CMain.Time + 3000;
                                        break;



                                    case Spell.MeteorStrike:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                        //BlizzardFreezeTime = CMain.Time + 3000;
                                        break;



                                    case Spell.Reincarnation:
                                        ReincarnationStopTime = 0;
                                        break;



                                    case Spell.SummonHolyDeva:
                                        Effects.Add(new Effect(Libraries.Magic, 1500, 10, Frame.Count * FrameInterval, this));
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                        break;



                                    case Spell.UltimateEnhancer:
                                        if (ob != null && ob != User)
                                            ob.Effects.Add(new Effect(Libraries.Magic2, 160, 15, 1000, ob));
                                        break;



                                    case Spell.Plague:
                                        //SoundManager.PlaySound(20000 + (ushort)Spell.SoulShield * 10);
                                        missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                        missile.Explode = true;

                                        missile.Complete += (o, e) =>
                                        {
                                            MapControl.Effects.Add(new Effect(Libraries.Magic3, 110, 10, 1200, TargetPoint));
                                            SoundManager.PlaySound(20000 + (ushort)Spell.Plague * 10 + 3);
                                        };
                                        break;



                                    case Spell.TrapHexagon:
                                        if (ob != null)
                                        SoundManager.PlaySound(20000 + (ushort)Spell.TrapHexagon * 10 + 1);
                                        break;



                                    case Spell.Trap:
                                        if (ob != null)
                                            SoundManager.PlaySound(20000 + (ushort)Spell.Trap * 10 + 1);
                                        break;



                                    case Spell.CrescentSlash:
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                        break;



                                    case Spell.NapalmShot:

                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                        MapControl.Effects.Add(new Effect(Libraries.Magic3, 1660, 10, 1000, TargetPoint));
                                        break;




                                    case Spell.FireBounce:
                                        SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 1);

                                        missile = CreateProjectile(410, Libraries.Magic, true, 6, 30, 4);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 2);
                                            };
                                        }
                                        break;



                                    case Spell.MeteorShower:

                                        SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 1);

                                        var targetIDs = new List<uint> { TargetID };

                                        if (SecondaryTargetIDs != null)
                                        {
                                            targetIDs.AddRange(SecondaryTargetIDs);
                                        }

                                        foreach (var targetID in targetIDs)
                                        {
                                            missile = CreateProjectile(410, Libraries.Magic, true, 6, 30, 4, targetID: targetID);

                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    var sender = (Missile)o;

                                                    if (sender.Target.CurrentAction == MirAction.Dead) return;
                                                    sender.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, sender.Target));
                                                    SoundManager.PlaySound(20000 + (ushort)Spell.GreatFireBall * 10 + 2);
                                                };
                                            }
                                        }
                                        
                                        break;


                                }


                                Cast = false;
                            }
                            //if (ActionFeed.Count == 0)
                            //    ActionFeed.Add(new QueuedAction { Action = MirAction.Stance, Direction = Direction, Location = CurrentLocation });

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();

                        }
                        else
                        {
                            NextMotion += FrameInterval;

                        }
                    }

                    UpdateWingEffect();
                    break;
                case MirAction.Die:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            ActionFeed.Clear();
                            ActionFeed.Add(new QueuedAction { Action = MirAction.Dead, Direction = Direction, Location = CurrentLocation });
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1)
                                PlayDieSound();

                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;
                case MirAction.Dead:
                    break;
                case MirAction.Revive:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            ActionFeed.Clear();
                            ActionFeed.Add(new QueuedAction { Action = MirAction.Standing, Direction = Direction, Location = CurrentLocation });
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }
                    break;

            }

            if (this == User) return;

            if ((CurrentAction == MirAction.Standing || CurrentAction == MirAction.MountStanding || CurrentAction == MirAction.Stance || CurrentAction == MirAction.Stance2 || CurrentAction == MirAction.DashFail) && NextAction != null)
                SetAction();
            //if Revive and dead set action

        }
        public int UpdateFrame(bool skip = true)
        {
            if (Frame == null) return 0;
            if (Poison.HasFlag(PoisonType.Slow) && !skip)
            {
                SkipFrameUpdate++;
                if (SkipFrameUpdate == 2)
                    SkipFrameUpdate = 0;
                else
                    return FrameIndex;
            }
            if (Frame.Reverse) return Math.Abs(--FrameIndex);

            return ++FrameIndex;
        }

        public int UpdateFrame2()
        {
            if (Frame == null) return 0;

            if (Frame.Reverse) return Math.Abs(--EffectFrameIndex);

            return ++EffectFrameIndex;
        }

        private void UpdateWingEffect()
        {
            if (WingEffect > 0 && CMain.Time >= NextMotion2)
            {
                GameScene.Scene.MapControl.TextureValid = false;

                if (SkipFrames) UpdateFrame2();

                if (UpdateFrame2() >= Frame.EffectCount)
                    EffectFrameIndex = Frame.EffectCount - 1;
                else
                    NextMotion2 += EffectFrameInterval;
            }
        }


        public override Missile CreateProjectile(int baseIndex, MLibrary library, bool blend, int count, int interval, int skip, int lightDistance = 6, bool direction16 = true, Color? lightColour = null, uint targetID = 0)
        {
            if (targetID == 0)
            {
                targetID = TargetID;
            }

            MapObject ob = MapControl.GetObject(targetID);

            var targetPoint = TargetPoint;

            if (ob != null) targetPoint = ob.CurrentLocation;

            int duration = Functions.MaxDistance(CurrentLocation, targetPoint) * 50;

            Missile missile = new Missile(library, baseIndex, duration / interval, duration, this, targetPoint)
            {
                Target = ob,
                Interval = interval,
                FrameCount = count,
                Blend = blend,
                Skip = skip,
                Light = lightDistance,
                LightColour = lightColour == null ? Color.White : (Color)lightColour
            };

            Effects.Add(missile);

            return missile;
        }

        //Rebuild
        /// <summary>
        /// 脚步声 —— 原始传奇做法：角色动作音由 ActionSound 绑定在动画帧上，
        /// 玩家只有跑步(action=3)在帧1播 RunSound(soundId=1)，走路不发声；
        /// 音效走 CDN audio/{id}.mp3（PlayOriginSound）。
        /// 旧实现是经典 Mir2 按 .map 瓦片材质(草地/石头/木板/洞穴)判断脚步声的近千行死代码，
        /// CDN 地图下相关字段全为 0、完全失效，已删除。
        /// </summary>
        public void PlayStepSound()
        {
            if (CurrentAction != MirAction.Running) return;   // 原版仅跑步有脚步声
            if (FrameIndex != 1 && FrameIndex != 4) return;  // 绑定在跑步动画的落脚帧
            if (RidingMount) return;                          // 坐骑另有音效
            SoundManager.PlayOriginSound(1);                  // ActionSound.RunSound
        }

        // 角色战斗音效统一改用原始传奇（ActionSound 权威定义，见主 JS）：
        //   RunSound=1 / AttackSound=2 / FemaleDieSound=3 / MaleDieSound=4；命中通用音=6（skillEffects）。
        // 经典 Mir2 那套按武器材质(剑/斧/棍)、性别、坐骑类型细分音色的 wav 属于被抛弃的旧资源体系，
        // 原始传奇没有这些细分，全部走 CDN audio/{id}.mp3（SoundManager.PlayOriginSound）。
        public void PlayStruckSound() => SoundManager.PlayOriginSound(6);   // 命中/受击通用音
        public void PlayFlinchSound() => SoundManager.PlayOriginSound(6);   // 受身退缩，同命中音
        public void PlayAttackSound() => SoundManager.PlayOriginSound(2);   // AttackSound
        public void PlayDieSound() => SoundManager.PlayOriginSound(Gender == 0 ? 4 : 3); // 男4/女3
        public void PlayMountSound() => SoundManager.PlayOriginSound(1);    // 坐骑移动，用跑步音

        public override void Draw()
        {
            DrawBehindEffects(Settings.Effect);

            float oldOpacity = DXManager.Opacity;
            if (Hidden && !DXManager.Blending) DXManager.SetOpacity(0.5F);

            // 原始传奇 CDN 独立图集：分层复合（法阵→足迹→坐骑→翅膀→身体→武器→旋光→称号→徽记），每层一个 origin model/{n}。
            // 身体/武器来自 cm.item[col17]（装扮/幻武可覆盖）；装扮类来自 cm.title[id].model。
            // 纯线上：origin 未就绪/缺失时不再回落 .Lib，直接跳过模型绘制（仅保留通用特效层）。
            if (_originModel > 0)
            {
                int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
                int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.DefaultTotalDir, (int)Direction);
                // 原始传奇 5 方向精灵：逻辑方向 5/6/7（左下/左/左上）复用右侧真帧 3/2/1，绘制时需水平翻转。
                bool mirror = (int)Direction >= 5;
                KFramework.MonoGame.SpriteEffects fx = mirror ? KFramework.MonoGame.SpriteEffects.FlipHorizontally : KFramework.MonoGame.SpriteEffects.None;
                // 局部：绘制一个 origin 层（请求+锚定绘制）。每层用自己的 standx/standy（来自 2config.modelInfo），
                // 各部件按自身枢轴对齐角色点、自然合成正确人形；不强制共享单一枢轴
                // （之前强制成盔甲注册点会把武器/护肩等彼此错开，模型“散架/画歪”）。
                // 头发/头部饰品应使用身体的枢轴（见 bodySx/bodySy），其余部件保持自身枢轴。
                void DrawLayer(int m, int? sx = null, int? sy = null)
                {
                    if (m <= 0) return;
                    int pr = (this == User) ? Mir2OriginObjectLibrary.PriorityUser : Mir2OriginObjectLibrary.PriorityVisible;
                    Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Player, m, action, dir, pr);
                    Mir2OriginObjectLibrary.DrawAnchored(Mir2ObjectKind.Player, m, action, dir, FrameIndex, DrawLocation.X, DrawLocation.Y, DrawColour, fx, standX: sx, standY: sy);
                }
                // 分层顺序（底→顶）：法阵→足迹→坐骑→翅膀→披风(后)→身体→护肩/玉佩/勋章/斗笠/面纱(覆盖)→武器→护盾(副手)→疗伤石→旋光/称号/徽记(顶)
                DrawLayer(PlayerZhenfaModel());    // 法阵（type5，地面法阵）
                DrawLayer(PlayerFootModel());      // 足迹（type2，脚下痕迹）
                DrawLayer(PlayerMountModel());     // 坐骑（底）
                DrawLayer(PlayerWingModel());      // 翅膀（后）
                // 身体层：主模型（盔甲/时装，由 _originModel 给出）若在 CDN 确认缺失，回落裸身 9001/9002。
                // 仍走 origin 独立图集（非旧 .Lib），保证玩家始终可见，而不是整人消失。
                int bodyModel = _originModel;
                if (Mir2OriginObjectLibrary.IsMissing(Mir2ObjectKind.Player, bodyModel, action, dir))
                {
                    int naked = Gender == MirGender.Female ? 9002 : 9001;
                    if (!_loggedBodyFallback.ContainsKey(bodyModel))
                    {
                        _loggedBodyFallback[bodyModel] = 1;
                        CMain.SaveError($"[玩家] 身体模型 {bodyModel} 在 CDN 缺失，回落裸身 {naked}（仍走 origin，非旧 .Lib）。");
                    }
                    bodyModel = naked;
                }
                // 角色主体层（身体/头发/头部饰品）以【身体】为枢轴基准对齐角色点（身体为准，头发跟随）。
                // 身体优先用自己的 stand；装备铠甲(9027)等缺 stand 时回落裸身 9001/9002 的 stand（必有），
                // 避免回落画布中心导致身体歪、而头发用自己的 stand 又与身体错开。
                // 头发/头部饰品（斗笠/面纱）跟随身体枢轴；披风为背后独立层，用自身枢轴（强制共用会让披风/武器错开、模型散架）。
                int baseBody = Gender == MirGender.Female ? 9002 : 9001;
                int charSx, charSy;
                if (!Mir2OriginObjectLibrary.TryGetStand(bodyModel, action, out charSx, out charSy)
                    && !Mir2OriginObjectLibrary.TryGetStand(baseBody, action, out charSx, out charSy))
                {
                    // 裸身也缺（如女性 9002 不在 stand 表）：用男裸身 9001 的枢轴作通用兜底（同虚拟画布，脚底基准一致）。
                    Mir2OriginObjectLibrary.TryGetStand(9001, action, out charSx, out charSy);
                }
                int hairModel = PlayerHairModel();
                DrawLayer(PlayerPiFengModel());                    // 披风（自身枢轴，不跟随身体）
                DrawLayer(bodyModel, charSx, charSy);              // 身体（中，含装扮覆盖）
                // 头发/护肩/玉佩/勋章/斗笠/面纱/武器/护盾：各自用自身 standx/standy，与身体按角色点自然对齐。
                DrawLayer(hairModel, charSx, charSy);              // 头发（叠在光头身体之上，位于斗笠/面纱之下）
                DrawLayer(PlayerHuJianModel());    // 护肩
                DrawLayer(PlayerYuPeiModel());     // 玉佩
                DrawLayer(PlayerXunZhangModel());  // 勋章（荣誉勋章）
                DrawLayer(PlayerDouLiModel(), charSx, charSy);     // 斗笠（头部，与身体同画布）
                DrawLayer(PlayerMianShaModel(), charSx, charSy);   // 面纱（面部，与身体同画布）
                DrawLayer(PlayerWeaponModel());    // 武器（前，含幻武覆盖）
                DrawLayer(PlayerHuDunModel());     // 护盾（副手）
                DrawLayer(PlayerLiaoShangModel()); // 疗伤石（无精灵，保留）— 自身枢轴
                DrawLayer(PlayerXuanGuangModel()); // 旋光（type7，环绕光效）— 自身枢轴
                DrawLayer(PlayerTitleModel());     // 称号（type3，头顶光环/称号）— 自身枢轴
                DrawLayer(PlayerHuiJiModel());     // 徽记（type4，徽印）— 自身枢轴

                // 鼠标命中框/血条定位用：与“身体实际绘制”用完全相同的参数（同 action / 同帧 / 同镜像 / 身体自己的枢轴）。
                // 各层已恢复各自 standx/standy，身体层用的就是它自己的注册点，命中框据此计算即贴合身体。
                // 名字定位已改到 MapObject.DrawName 里相对脚底基准 DrawLocation 固定定位，不依赖此处。
                if (Mir2OriginObjectLibrary.TryGetDrawRect(Mir2ObjectKind.Player, bodyModel, action, dir, FrameIndex,
                        DrawLocation.X, DrawLocation.Y, out var bodyRect, 1f, mirror: (int)Direction >= 5, standX: charSx, standY: charSy))
                {
                    DisplayRectangle = new Rectangle(bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);
                    DrawRectangleMarker(DisplayRectangle);
                }
                // 调试：在脚底基准 DrawLocation 画红圈，核对模型是否偏移。
                DrawLocationMarker(DrawLocation);
            }


            

            // 纯线上：origin 未就绪/缺失时不回落 .Lib，直接跳过模型绘制（通用特效层仍由 DrawBehindEffects/DrawEffects 负责）。
            DXManager.SetOpacity(oldOpacity);
        }

        public override void DrawBehindEffects(bool effectsEnabled)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (!Effects[i].DrawBehind) continue;
                if (!Settings.LevelEffect && (Effects[i] is SpecialEffect) && ((SpecialEffect)Effects[i]).EffectType == 1) continue;
                if ((!effectsEnabled) && (!IsVitalEffect(Effects[i]))) continue;
                Effects[i].Draw();
            }
        }

        public override void DrawEffects(bool effectsEnabled)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].DrawBehind) continue;
                if (!Settings.LevelEffect && (Effects[i] is SpecialEffect) && ((SpecialEffect)Effects[i]).EffectType == 1) continue;
                if ((!effectsEnabled) && (!IsVitalEffect(Effects[i]))) continue;
                Effects[i].Draw();
            }

            if (!effectsEnabled) return;

            switch (CurrentAction)
            {
                case MirAction.Attack1:
                    switch (Spell)
                    {
                        case Spell.Slaying:
                            Libraries.Magic.DrawBlend(1820 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.DoubleSlash:
                            Libraries.Magic2.DrawBlend(1960 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.Thrusting:
                            Libraries.Magic.DrawBlend(2190 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.HalfMoon:
                            Libraries.Magic.DrawBlend(2560 + ((int)Direction * 10) + SpellLevel * 90 + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.TwinDrakeBlade:
                            Libraries.Magic2.DrawBlend(220 + ((int)Direction * 20) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.CrossHalfMoon:
                            Libraries.Magic2.DrawBlend(40 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.FlamingSword:
                            Libraries.Magic.DrawBlend(3480 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                    }
                    break;
                case MirAction.Attack4:

                    switch (Spell)
                    {
                        case Spell.DoubleSlash:
                            Libraries.Magic2.DrawBlend(2050 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.TwinDrakeBlade:
                            Libraries.Magic2.DrawBlend(226 + ((int)Direction * 20) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                        case Spell.FlamingSword:
                            Libraries.Magic.DrawBlend(3480 + ((int)Direction * 10) + FrameIndex, DrawLocation, Color.White, true, 0.7F);
                            break;
                    }
                    break;
            }


        }

        public void DrawCurrentEffects()
        {
            if (CurrentEffect == SpellEffect.MagicShieldUp && !MagicShield)
            {
                MagicShield = true;
                Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                CurrentEffect = SpellEffect.None;
            }

            if (CurrentEffect == SpellEffect.ElementalBarrierUp && !ElementalBarrier)
            {
                ElementalBarrier = true;
                Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                CurrentEffect = SpellEffect.None;
            }

            if (ElementEffect > 0 && !HasElements)
            {
                HasElements = true;
                if (ElementEffect == 4)
                    Effects.Add(new ElementsEffect(Libraries.Magic3, 1660, 10, 10 * 100, this, true, 4, ElementOrbMax));
                else
                {
                    if (ElementEffect >= 1)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1630, 10, 10 * 100, this, true, 1, ElementOrbMax));
                    if (ElementEffect >= 2)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1640, 10, 10 * 100, this, true, 2, ElementOrbMax));
                    if (ElementEffect == 3)
                        Effects.Add(new ElementsEffect(Libraries.Magic3, 1650, 10, 10 * 100, this, true, 3, ElementOrbMax));
                }
                ElementEffect = 0;
            }
        }

        public override void DrawBlend()
        {
            DXManager.SetBlend(true, 0.3F);
            Draw();
            DXManager.SetBlend(false);
        }


        private bool IsVitalEffect(Effect effect)
        {
            if ((effect.Library == Libraries.Magic) && (effect.BaseIndex == 3890))
                return true;
            if ((effect.Library == Libraries.Magic3) && (effect.BaseIndex == 1890))
                return true;
            return false;
        }

        public void GetBackStepDistance(int magicLevel)
        {
            JumpDistance = 0;
            if (InTrapRock) return;

            int travel = 0;
            bool blocked = false;
            int dist = (magicLevel == 0) ? 1 : magicLevel;//3 max
            MirDirection jumpDir = Functions.ReverseDirection(Direction);

            Point location = CurrentLocation;
            for (int i = 0; i < dist; i++)//step 1t/m3
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!GameScene.Scene.MapControl.ValidPoint(location)) break;

                CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];
                if (cInfo.CellObjects != null)
                    for (int c = 0; c < cInfo.CellObjects.Count; c++)
                    {
                        MapObject ob = cInfo.CellObjects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.CellObjects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }
            JumpDistance = travel;
        }

        public void GetFlashDashDistance(int magicLevel)
        {
            JumpDistance = 0;
            if (InTrapRock) return;

            int travel = 0;
            bool blocked = false;
            int dist = (magicLevel <= 1) ? 0 : 1;
            MirDirection jumpDir = Direction;

            Point location = CurrentLocation;
            for (int i = 0; i < dist; i++)
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!GameScene.Scene.MapControl.ValidPoint(location)) break;

                CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];
                if (cInfo.CellObjects != null)
                    for (int c = 0; c < cInfo.CellObjects.Count; c++)
                    {
                        MapObject ob = cInfo.CellObjects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.CellObjects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }
            JumpDistance = travel;
        }

        public bool IsDashAttack()
        {
            Point location = CurrentLocation;
            location = Functions.PointMove(location, Direction, 1);

            if (!GameScene.Scene.MapControl.ValidPoint(location)) return false;

            CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];

            if (cInfo.CellObjects != null)
            {
                for (int c = 0; c < cInfo.CellObjects.Count; c++)
                {
                    MapObject ob = cInfo.CellObjects[c];
                    if (ob == this) return false;
                    switch (ob.Race)
                    {
                        case ObjectType.Monster:
                        case ObjectType.Player:
                            return true;
                    }
                }
            }

            return false;
        }

        public override bool MouseOver(Point p)
        {
            if (_originModel > 0)
            {
                int action = Mir2OriginObjectLibrary.MapAction(CurrentAction);
                int dir = Mir2OriginObjectLibrary.GetDir(Mir2OriginObjectLibrary.DefaultTotalDir, (int)Direction);
                if (Mir2OriginObjectLibrary.TryGetDrawRect(Mir2ObjectKind.Player, _originModel, action, dir, FrameIndex,
                                                          DrawLocation.X, DrawLocation.Y, out KFramework.MonoGame.Rectangle krect, 1f,
                                                          mirror: (int)Direction >= 5))
                    return krect.Contains(p.X, p.Y);
            }
            return MapControl.MapLocation == CurrentLocation || BodyLibrary != null && BodyLibrary.VisiblePixel(DrawFrame + ArmourOffSet, p.Subtract(FinalDrawLocation), false);
        }

        private void CreateNameLabel()
        {
            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != Name || LabelList[i].ForeColour != NameColour) continue;
                NameLabel = LabelList[i];
                break;
            }

            if (NameLabel != null && !NameLabel.IsDisposed) return;

            NameLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = Color.Black,
                Text = Name,
            };
            NameLabel.Disposing += (o, e) => LabelList.Remove(NameLabel);
            LabelList.Add(NameLabel);
        }

        private void CreateGuildLabel()
        {
            if (string.IsNullOrEmpty(GuildName))
                return;

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != GuildName || LabelList[i].ForeColour != NameColour) continue;
                GuildLabel = LabelList[i];
                break;
            }

            if (GuildLabel != null && !GuildLabel.IsDisposed) return;

            GuildLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = NameColour,
                OutLine = true,
                OutLineColour = Color.Black,
                Text = GuildName,
            };
            GuildLabel.Disposing += (o, e) => LabelList.Remove(GuildLabel);
            LabelList.Add(GuildLabel);
        }

        public override void CreateLabel()
        {
            NameLabel = null;
            GuildLabel = null;

            CreateNameLabel();
            CreateGuildLabel();
        }

        public override void DrawName()
        {
            CreateLabel();

            // 名字中心 = 脚底 - NameYOffset（身体中段），行会名在名字上方 16px（对应原始传奇 bloodY-32 vs -48）。
            int nameCenterY = DrawLocation.Y - NameYOffset + (Dead ? 35 : 0);

            if (GuildLabel != null && !string.IsNullOrEmpty(GuildName))
            {
                GuildLabel.Text = GuildName;
                GuildLabel.Location = new Point(DrawLocation.X - GuildLabel.Size.Width / 2, nameCenterY - 16 - GuildLabel.Size.Height / 2);
                GuildLabel.Draw();
            }

            if (NameLabel != null)
            {
                NameLabel.Text = Name;
                NameLabel.Location = new Point(DrawLocation.X - NameLabel.Size.Width / 2, nameCenterY - NameLabel.Size.Height / 2);
                NameLabel.Draw();
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
