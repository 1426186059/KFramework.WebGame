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
        public static GameScene Scene;
        public static bool Observing;
        public static bool AllowObserve;

        public static UserObject User
        {
            get { return MapObject.User; }
            set { MapObject.User = value; }
        }

        public static UserHeroObject Hero
        {
            get { return MapObject.Hero; }
            set { MapObject.Hero = value; }
        }
        public static HeroObject HeroObject
        {
            get { return MapObject.HeroObject; }
            set { MapObject.HeroObject = value; }
        }

        /// <summary>采样用：地图加载链路起点（收到 MapInformation / MapChanged 的时刻）。</summary>
        public static DateTime MapLoadStart;

        public static long MoveTime, AttackTime, NextRunTime, LogTime, LastRunTime, ChangePModeTime, ChangeAModeTime, HeroSpellTime, IntelligentCreaturePickupTime;
        public static bool CanMove, CanRun;

        private bool hasHero;
        public bool HasHero
        {
            get { return hasHero; }
            set
            {
                if (hasHero == value) return;

                hasHero = value;
                MainDialog.HeroSummonButton.Visible = value;
            }
        }
        public HeroSpawnState HeroSpawnState;
        public MapControl MapControl;
        public MainDialog MainDialog;
        public ChatDialog ChatDialog;
        public InventoryDialog InventoryDialog;
        public CharacterDialog CharacterDialog;
        public CharacterSkillDialog CharacterSkillDialog;
        public CharacterDialog HeroDialog;
        public HeroInventoryDialog HeroInventoryDialog;
        public HeroManageDialog HeroManageDialog;
        public CraftDialog CraftDialog;
        public StorageDialog StorageDialog;
        public BeltDialog BeltDialog;
        public MiniMapDialog MiniMapDialog;
        public InspectDialog InspectDialog;
        public OptionDialog OptionDialog;
        public MenuDialog MenuDialog;
        public NPCDialog NPCDialog;
        public NPCTeleporterDialog NPCTeleporterDialog;
        public NPCGoodsDialog NPCGoodsDialog;
        public NPCGoodsDialog NPCSubGoodsDialog;
        public NPCGoodsDialog NPCCraftGoodsDialog;
        public NPCDropDialog NPCDropDialog;
        public NPCAwakeDialog NPCAwakeDialog;
        public HelpDialog HelpDialog;
        public MountDialog MountDialog;
        public FishingDialog FishingDialog;
        public FishingStatusDialog FishingStatusDialog;
        public RefineDialog RefineDialog;

        public GroupDialog GroupDialog;
        public GuildDialog GuildDialog;
        public GuildTerritoryDialog GuildTerritoryDialog;
        public GMDialog GMDialog;
        public NewCharacterDialog NewHeroDialog;
        public HeroBeltDialog HeroBeltDialog;

        public BigMapDialog BigMapDialog;
        public TrustMerchantDialog TrustMerchantDialog;
        public TradeDialog TradeDialog;
        public GuestTradeDialog GuestTradeDialog;

        public HeroMenuPanel HeroMenuPanel;
        public HeroBehaviourPanel HeroBehaviourPanel;
        public SocketDialog SocketDialog;

        public SkillBarDialog SkillBarDialogs;
        public ChatOptionDialog ChatOptionDialog;
        public ChatNoticeDialog ChatNoticeDialog;

        public QuestListDialog QuestListDialog;
        public QuestDetailDialog QuestDetailDialog;
        public QuestDiaryDialog QuestLogDialog;
        public QuestTrackingDialog QuestTrackingDialog;
        public TaskCompleteEffect TaskCompleteEffect;
        public QuickActionDialog QuickActionDialog;

        public RankingDialog RankingDialog;

        public MailListDialog MailListDialog;
        public MailComposeLetterDialog MailComposeLetterDialog;
        public MailComposeParcelDialog MailComposeParcelDialog;
        public MailReadLetterDialog MailReadLetterDialog;
        public MailReadParcelDialog MailReadParcelDialog;

        public IntelligentCreatureDialog IntelligentCreatureDialog;
        public IntelligentCreatureOptionsDialog IntelligentCreatureOptionsDialog;
        public IntelligentCreatureOptionsGradeDialog IntelligentCreatureOptionsGradeDialog;

        public FriendDialog FriendDialog;
        public MemoDialog MemoDialog;
        public RelationshipDialog RelationshipDialog;
        public MentorDialog MentorDialog;
        public GameShopDialog GameShopDialog;

        public ReportDialog ReportDialog;

        public ItemRentingDialog ItemRentingDialog;
        public ItemRentDialog ItemRentDialog;
        public GuestItemRentingDialog GuestItemRentingDialog;
        public GuestItemRentDialog GuestItemRentDialog;
        public ItemRentalDialog ItemRentalDialog;

        public BuffDialog BuffsDialog;

        public KeyboardLayoutDialog KeyboardLayoutDialog;
        public WelcomeNoticeDialog WelcomeNoticeDialog;

        public TimerDialog TimerControl;
        public CompassDialog CompassControl;
        public RollDialog RollControl;


        public static List<ItemInfoTable> ItemInfoList = new List<ItemInfoTable>();
        public static List<UserId> UserIdList = new List<UserId>();
        public static List<UserItem> ChatItemList = new List<UserItem>();
        public static List<ClientQuestInfo> QuestInfoList = new List<ClientQuestInfo>();
        public static List<GameShopItem> GameShopInfoList = new List<GameShopItem>();
        public static List<ClientRecipeInfo> RecipeInfoList = new List<ClientRecipeInfo>();
        public static Dictionary<int, BigMapRecord> MapInfoList = new Dictionary<int, BigMapRecord>();
        public static List<ClientHeroInformation> HeroInfoList = new List<ClientHeroInformation>();
        public static ClientHeroInformation[] HeroStorage = new ClientHeroInformation[8];
        public static Dictionary<long, RankCharacterInfo> RankingList = new Dictionary<long, RankCharacterInfo>();
        public static int TeleportToNPCCost;
        public static int MaximumHeroCount;

        public static List<ClientMonsterInfo> MonsterInfoList = new List<ClientMonsterInfo>();
        public static List<ClientNPCInfo> NPCInfoList = new List<ClientNPCInfo>();
        private static readonly HashSet<int> RequestedItemInfo = new HashSet<int>();
        private static readonly HashSet<int> RequestedMonsterInfo = new HashSet<int>();
        private static readonly HashSet<int> RequestedNPCInfo = new HashSet<int>();

        public static void RequestItemInfo(int index)
        {
            if (index <= 0 || HasItemInfo(index) || !RequestedItemInfo.Add(index)) return;
            if (!Network.Connected) return;

            Network.Enqueue(new C.RequestItemInfo { ItemIndex = index });
        }

        public static void RequestMonsterInfo(int index)
        {
            if (index <= 0 || HasMonsterInfo(index) || !RequestedMonsterInfo.Add(index)) return;
            if (!Network.Connected) return;

            Network.Enqueue(new C.RequestMonsterInfo { MonsterIndex = index });
        }

        public static void RequestNPCInfo(int index)
        {
            if (index <= 0 || HasNPCInfo(index) || !RequestedNPCInfo.Add(index)) return;
            if (!Network.Connected) return;

            Network.Enqueue(new C.RequestNPCInfo { NPCIndex = index });
        }

        public static void OnItemInfoReceived(int index)
        {
            RequestedItemInfo.Remove(index);
        }

        public static void OnMonsterInfoReceived(int index)
        {
            RequestedMonsterInfo.Remove(index);
        }

        public static void OnNPCInfoReceived(int index)
        {
            RequestedNPCInfo.Remove(index);
        }

        private static bool HasItemInfo(int index)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i].Index == index)
                    return true;
            }

            return false;
        }

        private static bool HasMonsterInfo(int index)
        {
            for (int i = 0; i < MonsterInfoList.Count; i++)
            {
                if (MonsterInfoList[i].Index == index)
                    return true;
            }

            return false;
        }

        private static bool HasNPCInfo(int index)
        {
            for (int i = 0; i < NPCInfoList.Count; i++)
            {
                if (NPCInfoList[i].Index == index)
                    return true;
            }

            return false;
        }

        public static UserItem[] Storage = new UserItem[80];
        public static UserItem[] GuildStorage = new UserItem[112];
        public static UserItem[] Refine = new UserItem[16];
        public static UserItem HoverItem, SelectedItem;
        public static MirItemCell SelectedCell;

        public static bool PickedUpGold;
        public MirControl ItemLabel, MailLabel, MemoLabel, GuildBuffLabel;
        public static long UseItemTime, PickUpTime, DropViewTime, TargetDeadTime;
        // 三种货币：金币/元宝/传奇币，均不区分绑定与非绑定（绑定也并入同一字段）
        public static uint Gold, Credit, LegendCoin;
        public static long InspectTime;
        public bool ShowReviveMessage;


        public bool NewMail;
        public int NewMailCounter = 0;


        public AttackMode AMode;
        public PetMode PMode;

        public static long NPCTime;
        public static uint NPCID;
        public static float NPCRate;
        public static PanelType NPCPanelType;
        public static uint DefaultNPCID;
        public static bool HideAddedStoreStats;

        public long ToggleTime;
        public static long SpellTime;
        private readonly long[] _holdSkillNextTry = new long[25];

        public MirLabel[] OutputLines = new MirLabel[10];
        public List<OutPutMessage> OutputMessages = new List<OutPutMessage>();

        // 选中目标时屏幕正上方的血条面板（原始传奇 NorMonsterBloodSkin）
        private TargetHealthDialog _targetHealth;

        public long OutputDelay;

        public GameScene()
        {
            MapControl.AutoRun = false;
            MapControl.AutoHit = false;

            Scene = this;
            RequestedItemInfo.Clear();
            RequestedMonsterInfo.Clear();
            RequestedNPCInfo.Clear();

            //CommonColor.Transparent 会让 UI层 半透黑区域，变为 半透白。
            //这是原版也存在的问题，用默认的CommonColor.Empty 即可
            //BackColour = CommonColor.Transparent;
            BackColour = CommonColor.Empty;

            MoveTime = CMain.Time;

            KeyDown += GameScene_KeyDown;

            MainDialog = new MainDialog { Parent = this };
            ChatDialog = new ChatDialog { Parent = this };
            InventoryDialog = new InventoryDialog { Parent = this };
            BeltDialog = new BeltDialog { Parent = this };
            StorageDialog = new StorageDialog { Parent = this, Visible = false };
            CraftDialog = new CraftDialog { Parent = this, Visible = false };
            MiniMapDialog = new MiniMapDialog { Parent = this };
            InspectDialog = new InspectDialog { Parent = this, Visible = false };
            OptionDialog = new OptionDialog { Parent = this, Visible = false };
            MenuDialog = new MenuDialog { Parent = this, Visible = false };
            NPCDialog = new NPCDialog { Parent = this, Visible = false };
            NPCTeleporterDialog = new NPCTeleporterDialog { Parent = this, Visible = false };
            NPCTeleporterDialog.OnTeleport += d =>
            {
                // 复用现有 NPC 脚本机制：发 C.CallNPC 到对应的 @mv_xxx 页（服务端执行 MOVE），无需新增服务端包。
                Network.Enqueue(new C.CallNPC { ObjectID = NPCTeleporterDialog.CurrentNPCID, Key = "[@" + d.Action + "]" });
                NPCTeleporterDialog.Hide();
            };
            NPCGoodsDialog = new NPCGoodsDialog(PanelType.Buy) { Parent = this, Visible = false };
            NPCSubGoodsDialog = new NPCGoodsDialog(PanelType.BuySub) { Parent = this, Visible = false };
            NPCCraftGoodsDialog = new NPCGoodsDialog(PanelType.Craft) { Parent = this, Visible = false };
            NPCDropDialog = new NPCDropDialog { Parent = this, Visible = false };
            NPCAwakeDialog = new NPCAwakeDialog { Parent = this, Visible = false, Location = new Point(0, GameScene.Scene.NPCDialog.Size.Height) };

            HelpDialog = new HelpDialog { Parent = this, Visible = false };
            KeyboardLayoutDialog = new KeyboardLayoutDialog { Parent = this, Visible = false };
            WelcomeNoticeDialog = new WelcomeNoticeDialog { Parent = this, Visible = false };

            MountDialog = new MountDialog { Parent = this, Visible = false };
            FishingDialog = new FishingDialog { Parent = this, Visible = false };
            FishingStatusDialog = new FishingStatusDialog { Parent = this, Visible = false };

            GroupDialog = new GroupDialog { Parent = this, Visible = false };
            GuildDialog = new GuildDialog { Parent = this, Visible = false };
            GuildTerritoryDialog = new GuildTerritoryDialog { Parent = this, Visible = false };
            GMDialog = new GMDialog { Parent = this, Visible = false };
            // 左下角 GM 工具按钮：浏览器 WASM 下 Ctrl+G 常被浏览器/系统拦截，提供一个可点击的兜底入口。
            var gmButton = new MirButton
            {
                Parent = this,
                AutoSize = false, Size = new Size(36, 18),
                Location = new Point(8, Settings.ScreenHeight - 26),
                Border = true, BorderColour = CommonColor.Lime,
                ForeColour = new Color(20, 35, 12),
            };
            gmButton.Label.Text = "GM";
            gmButton.Label.ForeColour = CommonColor.White;
            gmButton.Click += (o, e) => GMDialog.Toggle();
            NewHeroDialog = new NewCharacterDialog { Parent = this, Visible = false };
            NewHeroDialog.TitleLabel.Index = 847;
            NewHeroDialog.TitleLabel.Location = new Point(246, 11);
            NewHeroDialog.OnCreateCharacter += (o, e) =>
            {
                Network.Enqueue(new C.NewHero
                {
                    Name = NewHeroDialog.NameTextBox.Text,
                    Class = NewHeroDialog.Class,
                    Gender = NewHeroDialog.Gender
                });
            };

            HeroMenuPanel = new HeroMenuPanel(this) { Visible = false };
            HeroBehaviourPanel = new HeroBehaviourPanel { Parent = this, Visible = false };
            HeroManageDialog = new HeroManageDialog { Parent = this, Visible = false };

            BigMapDialog = new BigMapDialog { Parent = this, Visible = false };
            TrustMerchantDialog = new TrustMerchantDialog { Parent = this, Visible = false };
            TradeDialog = new TradeDialog { Parent = this, Visible = false };
            GuestTradeDialog = new GuestTradeDialog { Parent = this, Visible = false };

            SocketDialog = new SocketDialog { Parent = this, Visible = false };

            SkillBarDialogs = new SkillBarDialog { Parent = this, Visible = false };
            ChatOptionDialog = new ChatOptionDialog { Parent = this, Visible = false };
            ChatNoticeDialog = new ChatNoticeDialog { Parent = this, Visible = false };

            QuestListDialog = new QuestListDialog { Parent = this, Visible = false };
            QuestDetailDialog = new QuestDetailDialog { Parent = this, Visible = false };
            QuestTrackingDialog = new QuestTrackingDialog { Parent = this, Visible = false };
            QuestLogDialog = new QuestDiaryDialog { Parent = this, Visible = false };
            TaskCompleteEffect = new TaskCompleteEffect { Parent = this };
            QuickActionDialog = new QuickActionDialog { Parent = this, Visible = false };

            RankingDialog = new RankingDialog { Parent = this, Visible = false };

            MailListDialog = new MailListDialog { Parent = this, Visible = false };
            MailComposeLetterDialog = new MailComposeLetterDialog { Parent = this, Visible = false };
            MailComposeParcelDialog = new MailComposeParcelDialog { Parent = this, Visible = false };
            MailReadLetterDialog = new MailReadLetterDialog { Parent = this, Visible = false };
            MailReadParcelDialog = new MailReadParcelDialog { Parent = this, Visible = false };

            IntelligentCreatureDialog = new IntelligentCreatureDialog { Parent = this, Visible = false };
            IntelligentCreatureOptionsDialog = new IntelligentCreatureOptionsDialog { Parent = this, Visible = false };
            IntelligentCreatureOptionsGradeDialog = new IntelligentCreatureOptionsGradeDialog { Parent = this, Visible = false };

            RefineDialog = new RefineDialog { Parent = this, Visible = false };
            RelationshipDialog = new RelationshipDialog { Parent = this, Visible = false };
            FriendDialog = new FriendDialog { Parent = this, Visible = false };
            MemoDialog = new MemoDialog { Parent = this, Visible = false };
            MentorDialog = new MentorDialog { Parent = this, Visible = false };
            GameShopDialog = new GameShopDialog { Parent = this, Visible = false };
            ReportDialog = new ReportDialog { Parent = this, Visible = false };

            ItemRentingDialog = new ItemRentingDialog { Parent = this, Visible = false };
            ItemRentDialog = new ItemRentDialog { Parent = this, Visible = false };
            GuestItemRentingDialog = new GuestItemRentingDialog { Parent = this, Visible = false };
            GuestItemRentDialog = new GuestItemRentDialog { Parent = this, Visible = false };
            ItemRentalDialog = new ItemRentalDialog { Parent = this, Visible = false };

            BuffsDialog = new BuffDialog
            {
                Parent = this,
                Visible = true,
            };

            KeyboardLayoutDialog = new KeyboardLayoutDialog { Parent = this, Visible = false };

            TimerControl = new TimerDialog { Parent = this, Visible = false };
            CompassControl = new CompassDialog { Parent = this, Visible = false };
            RollControl = new RollDialog { Parent = this, Visible = false };

            // 选中目标血条：屏幕正上方居中（原始 NorMonsterBloodSkin 移植）
            _targetHealth = new TargetHealthDialog
            {
                Parent = this,
            };

            for (int i = 0; i < OutputLines.Length; i++)
            {
                OutputLines[i] = new MirLabel
                {
                    AutoSize = true,
                    BackColour = CommonColor.Transparent,
                    Font = FontConst.F12,
                    ForeColour = CommonColor.White,
                    Location = new Point(20, 250 + i * 21),
                    OutLine = true,
                };
            }

            if (MapInfoList.Count > 0)
            {
                RecreateBigMapButtons();
            }

            CMain.ShowLoadingDialog();   // 触发创建全屏加载蒙层（由 CMain 在场景绘制之后单独 Draw，层级最高）
        }

        private void UpdateMouseCursor()
        {
            if (!Settings.UseMouseCursors) return;

            if (GameScene.HoverItem != null)
            {
                if (GameScene.SelectedCell != null && GameScene.SelectedCell.Item != null && GameScene.SelectedCell.Item.Info.Type == ItemType.Gem && CMain.Ctrl)
                {
                    CMain.SetMouseCursor(MouseCursor.Upgrade);
                }
                else
                {
                    CMain.SetMouseCursor(MouseCursor.Default);
                }
            }
            else if (MapObject.MouseObject != null)
            {
                switch (MapObject.MouseObject.Race)
                {
                    case ObjectType.Monster:
                        CMain.SetMouseCursor(MouseCursor.Attack);
                        break;
                    case ObjectType.Merchant:
                        CMain.SetMouseCursor(MouseCursor.NPCTalk);
                        break;
                    case ObjectType.Player:
                        if (CMain.Shift)
                        {
                            CMain.SetMouseCursor(MouseCursor.AttackRed);
                        }
                        else
                        {
                            CMain.SetMouseCursor(MouseCursor.Default);
                        }
                        break;
                    default:
                        CMain.SetMouseCursor(MouseCursor.Default);
                        break;
                }
            }
            else
            {
                CMain.SetMouseCursor(MouseCursor.Default);
            }

        }

        public void OutputMessage(string message, OutputMessageType type = OutputMessageType.Normal)
        {
            // 左侧提示统一左对齐：去掉消息开头可能带入的空格/制表符/全角空格。
            message = message?.TrimStart(' ', '\t', '　') ?? string.Empty;

            OutputMessages.Add(new OutPutMessage { Message = message, ExpireTime = CMain.Time + 5000, Type = type });
            if (OutputMessages.Count > 10)
                OutputMessages.RemoveAt(0);
        }

        private void ProcessOuput()
        {
            for (int i = 0; i < OutputMessages.Count; i++)
            {
                if (CMain.Time >= OutputMessages[i].ExpireTime)
                    OutputMessages.RemoveAt(i);
            }

            for (int i = 0; i < OutputLines.Length; i++)
            {
                if (OutputMessages.Count > i)
                {
                    Color color;
                    switch (OutputMessages[i].Type)
                    {
                        case OutputMessageType.Quest:
                            color = CommonColor.Gold;
                            break;
                        case OutputMessageType.Guild:
                            color = CommonColor.DeepPink;
                            break;
                        case OutputMessageType.Error:
                            color = CommonColor.Red;
                            break;
                        default:
                            color = CommonColor.White;
                            break;
                    }

                    OutputLines[i].Text = OutputMessages[i].Message;
                    OutputLines[i].ForeColour = color;
                    OutputLines[i].Visible = true;
                }
                else
                {
                    OutputLines[i].Text = string.Empty;
                    OutputLines[i].Visible = false;
                }
            }
        }

    }

}