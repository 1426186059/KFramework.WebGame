using Client.MirScenes;
using Client.MirScenes.Dialogs;
using WebGame.Mir2.MonoGame.Client;
using S = ServerPackets;

namespace Client.MirObjects
{
    public partial class UserObject : PlayerObject
    {
        public uint Id;

        public int HP, MP;

        public int AttackSpeed;

        public Stats Stats;

        public long Experience, MaxExperience;
        public uint SExp; // 10倍经验点剩余数量

        public bool TradeLocked;
        public uint TradeGoldAmount;
        public bool AllowTrade;

        public bool RentalGoldLocked;
        public bool RentalItemLocked;
        public uint RentalGoldAmount;

        public SpecialItemMode ItemMode;

        public BaseStats CoreStats = new BaseStats(0);

        public virtual BuffDialog GetBuffDialog => GameScene.Scene.BuffsDialog;

        public UserItem[] Inventory = new UserItem[InventoryLayout.InitialSlots], Equipment = new UserItem[Enum.GetNames(typeof(EquipmentSlot)).Length], Trade = new UserItem[10], QuestInventory = new UserItem[40];
        public int BeltIdx = InventoryLayout.BeltSlots, HeroBeltIdx = 2;
        public bool HasExpandedStorage = false;
        public bool HasStoragePassword = false;
        public bool RequireStoragePassword = true;
        public DateTime StoragePasswordLastSet;
        public DateTime ExpandedStorageExpiryTime;

        public List<ClientMagic> Magics = new List<ClientMagic>();
        public List<ItemSets> ItemSets = new List<ItemSets>();
        public List<EquipmentSlot> MirSet = new List<EquipmentSlot>();

        public List<ClientIntelligentCreature> IntelligentCreatures = new List<ClientIntelligentCreature>();
        public IntelligentCreatureType SummonedCreatureType = IntelligentCreatureType.None;
        public bool CreatureSummoned;
        public int PearlCount = 0;

        public List<ClientQuestProgress> CurrentQuests = new List<ClientQuestProgress>();
        public List<int> CompletedQuests = new List<int>();
        // 登录时服务端会发一次全量 CompletedList 初始化列表；那不是"新完成任务"，不能播完成特效，
        // 否则历史所有已完成任务都会被当成新增、一次排队弹几十个特效。收到第一包后置 true。
        public bool CompletedListInitialized;
        // 已开放（未接取）任务：左上角显示“寻找 NPC”，玩家前往后才正式接取。
        public List<ClientQuestProgress> OfferedQuests = new List<ClientQuestProgress>();
        public List<ClientMail> Mail = new List<ClientMail>();

        public bool Slaying, Thrusting, HalfMoon, CrossHalfMoon, DoubleSlash, TwinDrakeBlade, FlamingSword;
        public ClientMagic NextMagic;
        public Point NextMagicLocation;
        public MirDirection NextMagicDirection;
        public QueuedAction QueuedAction;

        public UserObject() { }
        public UserObject(uint objectID) : base(objectID)
        {
            Stats = new Stats();
        }

        public virtual void Load(S.UserInformation info)
        {
            Id = info.RealId;
            Name = info.Name;
            NameColour = info.NameColour;
            GuildName = info.GuildName;
            GuildRankName = info.GuildRank;
            Class = info.Class;
            Gender = info.Gender;
            Level = info.Level;

            // 纯线上：本地玩家也必须从 Equipment 反推并设置 origin 分层模型号，否则 _originModel=0 会跳过整个 origin 绘制分支（整人黑屏）。
            SetUserOriginModels(info.Equipment);

            CurrentLocation = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);

            Direction = info.Direction;
            Hair = info.Hair;

            HP = info.HP;
            MP = info.MP;

            Experience = info.Experience;
            MaxExperience = info.MaxExperience;
            SExp = info.SExp;

            LevelEffects = info.LevelEffects;

            Inventory = info.Inventory;
            Equipment = info.Equipment;
            QuestInventory = info.QuestInventory;

            HasExpandedStorage = info.HasExpandedStorage;
            HasStoragePassword = info.HasStoragePassword;
            RequireStoragePassword = info.RequireStoragePassword;
            StoragePasswordLastSet = info.StoragePasswordLastSet;
            ExpandedStorageExpiryTime = info.ExpandedStorageExpiryTime;

            Magics = info.Magics;
            for (int i = 0; i < Magics.Count; i++ )
            {
                Magics[i].CastTime += CMain.Time;
            }

            // 已学技能列表到这里才到位：SetUserOriginModels 在第 85 行先于 Magics 赋值执行，
            // 那次预载遍历的是空列表；这里补一次全量预载（高优先级、各方向），否则之后被去重逻辑跳过永不加载。
            PreLoadSelfMagicEffects();

            IntelligentCreatures = info.IntelligentCreatures;
            SummonedCreatureType = info.SummonedCreatureType;
            CreatureSummoned = info.CreatureSummoned;

            BindAllItems();

            RefreshStats();

            SetAction();
        }

        public void SetSlots(S.UserSlotsRefresh p)
        {
            Inventory = p.Inventory;
            Equipment = p.Equipment;

            BindAllItems();
            SetUserOriginModels(Equipment);   // 装备变化后刷新 origin 分层模型号（与 Load 保持一致）
            RefreshStats();
        }

        public override void SetLibraries()
        {
            base.SetLibraries();
        }

        public override void SetEffects()
        {
            base.SetEffects();
        }

        public override void SetAction()
        {
            if (QueuedAction != null && !GameScene.Observing)
            {
                if ((ActionFeed.Count == 0) || (ActionFeed.Count == 1 && NextAction.Action == MirAction.Stance))
                {
                    ActionFeed.Clear();
                    ActionFeed.Add(QueuedAction);
                    QueuedAction = null;
                }
            }

            base.SetAction();
        }
        public override void ProcessFrames()
        {
            bool clear = CMain.Time >= NextMotion;

            base.ProcessFrames();

            if (clear) QueuedAction = null;
            if ((CurrentAction == MirAction.Standing || CurrentAction == MirAction.MountStanding || CurrentAction == MirAction.Stance || CurrentAction == MirAction.Stance2 || CurrentAction == MirAction.DashFail) && (QueuedAction != null || NextAction != null))
                SetAction();
        }

    }
}
