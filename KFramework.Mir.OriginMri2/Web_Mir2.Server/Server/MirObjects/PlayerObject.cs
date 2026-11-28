// partial 主文件：PlayerObject 字段与类型头，方法按功能拆到同名 partial 文件。
using System.Drawing;
using C = ClientPackets;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using S = ServerPackets;
using System.Text.RegularExpressions;
using Timer = Server.MirEnvir.Timer;


namespace Server.MirObjects
{
    public partial class PlayerObject : HumanObject
    {
        private long NextTradeTime;
        private long NextGroupInviteTime;

        public string GMPassword = Settings.GMPassword;
        public bool GMLogin, EnableGroupRecall, EnableGuildInvite, AllowMarriage, AllowLoverRecall, AllowMentor, HasMapShout, HasServerShout; //TODO - Remove        

        public long LastRecallTime, LastTeleportTime, LastProbeTime;
        public long NextMailTime;
        public long MenteeEXP;

        public bool WarZone = false;

        public int CurrentHeroIndex;
        private HeroInfo currentHero;
        public HeroInfo CurrentHero
        {
            get { return currentHero; }
            set
            {
                currentHero = value;

                if (currentHero != null)
                {
                    Info.CurrentHeroIndex = currentHero.Index;
                    for (int i = 0; i < Info.Heroes.Length; i++)
                    {
                        if (Info.Heroes[i].Index != currentHero.Index) continue;
                        CurrentHeroIndex = i;
                        break;
                    }
                }
                else
                {
                    Info.CurrentHeroIndex = 0;
                    CurrentHeroIndex = -1;
                }

            }
        }
        public HeroObject Hero;

        protected AccountInfo account;
        public virtual AccountInfo Account
        {
            get { return account; }
            set { account = value; }
        }

        public override bool CanMove
        {
            get
            {
                return base.CanMove && !Fishing;
            }
        }
        public override bool CanWalk
        {
            get
            {
                return base.CanMove && !Fishing;
            }
        }
        public override bool CanAttack
        {
            get
            {
                return base.CanAttack && !Fishing;
            }
        }
        /// <summary>在 HumanObject.CanCast 基础上叠加"钓鱼时不能施法"（钓鱼占用动作，但不属于战斗状态）。</summary>
        protected override bool CanCast
        {
            get
            {
                return base.CanCast && !Fishing;
            }
        }

        public bool NewMail = false;

        public override int PKPoints
        {
            get { return Info.PKPoints; }
            set { Info.PKPoints = value; }
        }

        public int BindMapIndex
        {
            get { return Info.BindMapIndex; }
            set { Info.BindMapIndex = value; }
        }
        public Point BindLocation
        {
            get { return Info.BindLocation; }
            set { Info.BindLocation = value; }
        }

        public int FishingChance, FishingChanceCounter, FishingProgressMax, FishingProgress, FishingAutoReelChance = 0, FishingNibbleChance = 0;
        public bool Fishing, FishingAutocast, FishFound, FishFirstFound;

        public const long TurnDelay = 350, HarvestDelay = 350, FishingCastDelay = 750, FishingDelay = 200, MovementDelay = 2000;
        public long ChatTime, ShoutTime, FishingTime, FishingFoundTime, CreatureTimeLeftTicker, RestedTime, MovementTime;

        public byte ChatTick;

        public bool SendIntelligentCreatureUpdates = false;

        protected int _fishCounter, _restedCounter;

        public uint NPCObjectID;
        public int NPCScriptID;
        public NPCPage NPCPage;
        public Dictionary<NPCSegment, bool> NPCSuccess = new Dictionary<NPCSegment, bool>();
        public bool NPCDelayed;
        // 原始传奇 checknpcTaskDialog：任务追踪点击 NPC 时置位，下一次 S.NPCResponse 消费它，
        // 让客户端弹「NPC 任务面板」而非普通对话面板。一次性标记，发完即清。
        public bool OpenQuestPanelOnNextNPCResponse;
        public List<string> NPCSpeech = new List<string>();
        public Dictionary<string, object> NPCData = new Dictionary<string, object>();

        private bool StorageUnlocked;

        public bool UserMatch;
        public string MatchName;
        public ItemType MatchType;
        public MarketPanelType MarketPanelType;
        public short MinShapes, MaxShapes;

        public int PageSent;
        public List<AuctionInfo> Search = new List<AuctionInfo>();

        public bool CanCreateGuild = false;

        public GuildObject PendingGuildInvite = null;
        public bool GuildNoticeChanged = true; //set to false first time client requests notice list, set to true each time someone in guild edits notice
        public bool GuildMembersChanged = true;//same as above but for members
        public bool GuildCanRequestItems = true;
        public bool RequestedGuildBuffInfo = false;

        public bool CanCreateHero = false;

        public bool AllowGroup
        {
            get { return Info.AllowGroup; }
            set { Info.AllowGroup = value; }
        }

        public bool AllowTrade
        {
            get { return Info.AllowTrade; }
            set { Info.AllowTrade = value; }
        }

        public bool AllowObserve
        {
            get { return Info.AllowObserve; }
            set { Info.AllowObserve = value; }
        }

        public PlayerObject MarriageProposal;
        public PlayerObject DivorceProposal;
        public PlayerObject MentorRequest;

        public PlayerObject GroupInvitation;
        public PlayerObject GroupOwner;
        public PlayerObject TradeInvitation;

        public PlayerObject TradePartner = null;
        public bool TradeLocked = false;
        public uint TradeGoldAmount = 0;

        public PlayerObject ItemRentalPartner = null;
        public UserItem ItemRentalDepositedItem = null;
        public uint ItemRentalFeeAmount = 0;
        public uint ItemRentalPeriodLength = 0;
        public bool ItemRentalFeeLocked = false;
        public bool ItemRentalItemLocked = false;

        private long LastRankUpdate = Envir.Time;

        private Map LastValidMap;
        private Point LastValidLocation;

        public List<QuestProgressInfo> CurrentQuests
        {
            get { return Info.CurrentQuests; }
        }

        public List<int> CompletedQuests
        {
            get { return Info.CompletedQuests; }
        }

        public List<int> TrackedQuests
        {
            get { return Info.TrackedQuests; }
        }

    }
}
