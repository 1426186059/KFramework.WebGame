using ClientPackets;
using Server.Library.MirDatabase;
using Server.Library.Utils;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using Server.MirObjects.Monsters;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using Library.Network;
using S = ServerPackets;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.MirEnvir
{
    public class MobThread
    {
        public int Id = 0;
        public long LastRunTime = 0;
        public long StartTime = 0;
        public long EndTime = 0;
        public LinkedList<MapObject> ObjectsList = new LinkedList<MapObject>();
        public LinkedListNode<MapObject> _current = null;
        public bool Stop = false;
    }
    public class RandomProvider
    {
        private static int seed = Environment.TickCount;
        private static readonly ThreadLocal<Random> RandomWrapper = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static Random GetThreadRandom() =>
            RandomWrapper.Value;

        public int Next() =>
            RandomWrapper.Value.Next();
        public int Next(int maxValue) =>
            RandomWrapper.Value.Next(maxValue);
        public int Next(int minValue, int maxValue) =>
            RandomWrapper.Value.Next(minValue, maxValue);
    }

    public partial class Envir
    {
        public static Envir Main { get; } = new Envir();

        public static Envir Edit { get; } = new Envir();

        // 编辑器实例(EditEnvir)与运行时实例(Main)都会 LoadDB 并把日志写进同一 MessageQueue，
        // 用该标识区分两条看似重复的加载日志（不是 bug）。
        private string InstanceTag => ReferenceEquals(this, Edit) ? "编辑器" : "运行时";

        protected static MessageQueue MessageQueue => MessageQueue.Instance;

        public static object AccountLock = new object();
        public static object LoadLock = new object();

        public const int MinVersion = 60;
        public const int Version = 120;
        public const int CustomVersion = 0;
        public static readonly string AccountPath = Path.Combine(Settings.VersionRoot, "Server.MirADB");
        public static readonly string BackUpPath = Path.Combine(".", "Back Up");
        public static readonly string AccountsBackUpPath = Path.Combine(".", "Back Up", "Accounts");
        public static readonly string ArchivePath = Path.Combine(".", "Archive");
        public bool ResetGS = false;
        public bool GuildRefreshNeeded;

        private static readonly Regex AccountIDReg, PasswordReg, EMailReg, CharacterReg;

        public static int LoadVersion;
        public static int LoadCustomVersion;

        private readonly DateTime _startTime = DateTime.UtcNow;
        public readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        public long Time { get; private set; }
        public RespawnTimerTable RespawnTick = new RespawnTimerTable();

        private static List<string> DisabledCharNames = new List<string>();
        private static List<string> LineMessages = new List<string>();

        public static ConcurrentDictionary<string, DateTime> IPBlocks = new ConcurrentDictionary<string, DateTime>();

        public static ConcurrentDictionary<string, MirConnectionLog> ConnectionLogs = new ConcurrentDictionary<string, MirConnectionLog>();

        public DateTime Now =>
            _startTime.AddMilliseconds(Time);

        public bool Running { get; private set; }


        private static uint _objectID;
        public uint ObjectID => ++_objectID;

        public static int _playerCount;
        public int PlayerCount => Players.Count;

        public int[] OnlineRankingCount = new int[6];
        public int HeroCount => Heroes.Count;

        public RandomProvider Random = new RandomProvider();

        private Thread _thread;
        private TcpListener _listener;
        private bool StatusPortEnabled = true;
        public List<MirStatusConnection> StatusConnections = new List<MirStatusConnection>();
        private TcpListener _StatusPort;
        private int _sessionID;
        public List<MirConnection> Connections = new List<MirConnection>();

        //Server DB
        public int MapIndex, ItemIndex, MonsterIndex, NPCIndex, QuestIndex, GameshopIndex, ConquestIndex, RespawnIndex, ScriptIndex;
        public List<MapInfoTable> MapInfoList = new List<MapInfoTable>();
        public List<ItemInfoTable> ItemInfoList = new List<ItemInfoTable>();
        public List<MonsterInfoTable> MonsterInfoList = new List<MonsterInfoTable>();
        public List<MagicInfoTable> MagicInfoList = new List<MagicInfoTable>();
        public List<NPCInfoTable> NPCInfoList = new List<NPCInfoTable>();

        /// <summary>mapnpc 表：NPC id → 全部摆放点。由 LoadMapNpcPlacements 从 5config 分片读取。</summary>
        public Dictionary<int, List<NPCPlacement>> MapNpcPlacements = new Dictionary<int, List<NPCPlacement>>();

        /// <summary>原始传奇商店表：StoreId → 商品记录列表（4config.npcstore，由 LoadOriginStores 加载）。</summary>
        public Dictionary<int, List<OriginStoreItemTable>> OriginStores = new Dictionary<int, List<OriginStoreItemTable>>();
        /// <summary>原始传奇显示条件表（0config.conditions，id → 条件），用于 npcstore.showCondition 过滤。</summary>
        public Dictionary<int, OriginConditionTable> OriginConditions = new Dictionary<int, OriginConditionTable>();

        // ===== 原始传奇任务权威表（4config._task / 5config.goals / 0config.global）：追踪面板文字直接读这些表 =====
        /// <summary>原始传奇任务主表 4config._task，key = 任务 id（name / completeinfo / goals / type / fnpc / tnpc）。</summary>
        public Dictionary<int, OriginTaskTable> OriginTasks = new Dictionary<int, OriginTaskTable>();
        /// <summary>原始传奇任务目标表 5config.goals，key = goal id（goInfo / type / count / param / mapId / x / y）。</summary>
        public Dictionary<int, OriginGoalTable> OriginGoals = new Dictionary<int, OriginGoalTable>();
        /// <summary>原始传奇任务类型名前缀 0config.global[9201]，key = type，value = "[主线]" 等前缀。</summary>
        public Dictionary<int, string> OriginTaskTypeNames = new Dictionary<int, string>();
        /// <summary>原始传奇富文本样式表 5config.textstyle（Logic.getCol(id)），key = 样式 id，任务追踪取 31(白)/32(绿)。</summary>
        public Dictionary<int, OriginTextStyleTable> OriginTextStyles = new Dictionary<int, OriginTextStyleTable>();

        public DragonInfoTable DragonInfoTable = new DragonInfoTable();
        public List<QuestInfoTable> QuestInfoList = new List<QuestInfoTable>();
        public List<GameShopItemTable> GameShopList = new List<GameShopItemTable>();
        public List<RecipeInfo> RecipeInfoList = new List<RecipeInfo>();
        public List<BuffInfo> BuffInfoList = new List<BuffInfo>();
        public List<ConquestInfoTable> ConquestInfoList = new List<ConquestInfoTable>();
        public List<GTMap> GTMapList = new List<GTMap>();

        //User DB
        public int NextAccountID, NextCharacterID, NextGuildID, NextHeroID;
        public ulong NextUserItemID, NextAuctionID, NextMailID, NextRecipeID;
        public List<AccountInfo> AccountList = new List<AccountInfo>();
        public List<CharacterInfo> CharacterList = new List<CharacterInfo>();
        public List<GuildInfo> GuildList = new List<GuildInfo>();
        public LinkedList<AuctionInfo> Auctions = new LinkedList<AuctionInfo>();
        public List<ConquestGuildInfo> ConquestList = new List<ConquestGuildInfo>();
        public Dictionary<int, int> GameshopLog = new Dictionary<int, int>();
        public List<HeroInfo> HeroList = new List<HeroInfo>();

        public int GuildCount; //This shouldn't be needed?? -> remove in the future

        //Live Info
        public bool Saving = false;
        public List<Map> MapList = new List<Map>();
        public List<SafeZoneInfo> StartPoints = new List<SafeZoneInfo>();
        public List<ItemInfoTable> StartItems = new List<ItemInfoTable>();

        public List<PlayerObject> Players = new List<PlayerObject>();
        public List<SpellObject> Spells = new List<SpellObject>();
        public List<NPCObject> NPCs = new List<NPCObject>();
        public List<GuildObject> Guilds = new List<GuildObject>();
        public List<ConquestObject> Conquests = new List<ConquestObject>();
        public List<HeroObject> Heroes = new List<HeroObject>();

        public LightSetting Lights;
        public LinkedList<MapObject> Objects = new LinkedList<MapObject>();
        public Dictionary<int, NPCScript> Scripts = new Dictionary<int, NPCScript>();
        public Dictionary<string, Timer> Timers = new Dictionary<string, Timer>();

        //multithread vars
        readonly object _locker = new object();
        public MobThread[] MobThreads = new MobThread[Settings.ThreadLimit];
        private readonly Thread[] MobThreading = new Thread[Settings.ThreadLimit];
        public int SpawnMultiplier = 1;//set this to 2 if you want double spawns (warning this can easily lag your server far beyond what you imagine)

        public List<string> CustomCommands = new List<string>();

        public Dragon DragonSystem;
        public NPCScript DefaultNPC, MonsterNPC, RobotNPC;

        public List<DropInfo> FishingDrops = new List<DropInfo>();
        public List<DropInfo> AwakeningDrops = new List<DropInfo>();

        public List<DropInfo> StrongboxDrops = new List<DropInfo>();
        public List<DropInfo> BlackstoneDrops = new List<DropInfo>();

        public List<GuildAtWar> GuildsAtWar = new List<GuildAtWar>();
        public List<MapRespawn> SavedSpawns = new List<MapRespawn>();

        public List<RankCharacterInfo> RankTop = new List<RankCharacterInfo>();
        public List<RankCharacterInfo>[] RankClass = new List<RankCharacterInfo>[5];

        static HttpServer http;

    }
}
