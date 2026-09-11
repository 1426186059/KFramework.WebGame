using SlimDX;
using SlimDX.Direct3D9;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Frame = Client.MirObjects.Frame;
using Client.MirObjects;
using System.Text.RegularExpressions;
using MirEngine;

namespace Client.MirGraphics
{
    public static class Libraries
    {
        public static bool Loaded;
        public static int Count, Progress;

        /// <summary>某个库完成异步加载后触发。用于在资源就绪后令活动场景重绘（重烘焙离屏纹理），
        /// 否则场景会在库加载前完成一次性烘焙并卡在默认背景色（登录界面表现为整屏粉红色）。</summary>
        public static event Action LibraryLoaded;
        internal static void OnLibraryLoaded() => LibraryLoaded?.Invoke();

        /// <summary>待加载库队列：静态构造阶段只登记，后续由 LoadQueuedAsync 并发限流加载。</summary>
        private static readonly List<MLibrary> _queue = new List<MLibrary>();

        /// <summary>浏览器同域并发连接上限（HTTP/1.1 约 6），超过会排队拖慢整体加载。</summary>
        private const int MaxConcurrentLoads = 6;

        public static readonly MLibrary
            ChrSel = new MLibrary(Settings.DataPath + "ChrSel"),
            Prguse = new MLibrary(Settings.DataPath + "Prguse"),
            Prguse2 = new MLibrary(Settings.DataPath + "Prguse2"),
            Prguse3 = new MLibrary(Settings.DataPath + "Prguse3"),
            UI_32bit = new MLibrary(Settings.DataPath + "UI_32bit"),
            BuffIcon = new MLibrary(Settings.DataPath + "BuffIcon"),
            Help = new MLibrary(Settings.DataPath + "Help"),
            MiniMap = new MLibrary(Settings.DataPath + "MMap"),
            MapLinkIcon = new MLibrary(Settings.DataPath + "MapLinkIcon"),
            Title = new MLibrary(Settings.DataPath + "Title"),
            MagIcon = new MLibrary(Settings.DataPath + "MagIcon"),
            MagIcon2 = new MLibrary(Settings.DataPath + "MagIcon2"),
            Magic = new MLibrary(Settings.DataPath + "Magic"),
            Magic2 = new MLibrary(Settings.DataPath + "Magic2"),
            Magic3 = new MLibrary(Settings.DataPath + "Magic3"),
            Effect = new MLibrary(Settings.DataPath + "Effect"),
            MagicC = new MLibrary(Settings.DataPath + "MagicC"),
            GuildSkill = new MLibrary(Settings.DataPath + "GuildSkill"),
            Weather = new MLibrary(Settings.DataPath + "Weather");

        public static readonly MLibrary
            Background = new MLibrary(Settings.DataPath + "Background");


        public static readonly MLibrary
            Dragon = new MLibrary(Settings.DataPath + "Dragon");

        //Map
        public static readonly MLibrary[] MapLibs = new MLibrary[400];

        //Items
        public static readonly MLibrary
            Items = new MLibrary(Settings.DataPath + "Items"),
            StateItems = new MLibrary(Settings.DataPath + "StateItem"),
            FloorItems = new MLibrary(Settings.DataPath + "DNItems"),
            Items_Tooltip_32bit = new MLibrary(Settings.DataPath + "Items_Tooltip_32bit");

        //Deco
        public static readonly MLibrary
            Deco = new MLibrary(Settings.DataPath + "Deco");

        public static MLibrary[] CArmours,
                                          CWeapons,
										  CWeaponEffect,
										  CHair,
                                          CHumEffect,
                                          AArmours,
                                          AWeaponsL,
                                          AWeaponsR,
                                          AHair,
                                          AHumEffect,
                                          ARArmours,
                                          ARWeapons,
                                          ARWeaponsS,
                                          ARHair,
                                          ARHumEffect,
                                          Monsters,
                                          Gates,
                                          Flags,
                                          Siege,
                                          Mounts,
                                          NPCs,
                                          Fishing,
                                          Pets,
                                          Transform,
                                          TransformMounts,
                                          TransformEffect,
                                          TransformWeaponEffect;

        static Libraries()
        {
            //Wiz/War/Tao
            InitLibrary(ref CArmours, Settings.CArmourPath, "00");
            InitLibrary(ref CHair, Settings.CHairPath, "00");
            InitLibrary(ref CWeapons, Settings.CWeaponPath, "00");
            InitLibrary(ref CWeaponEffect, Settings.CWeaponEffectPath, "00");
            InitLibrary(ref CHumEffect, Settings.CHumEffectPath, "00");

            //Assassin
            InitLibrary(ref AArmours, Settings.AArmourPath, "00");
            InitLibrary(ref AHair, Settings.AHairPath, "00");
            InitLibrary(ref AWeaponsL, Settings.AWeaponPath, "00", " L");
            InitLibrary(ref AWeaponsR, Settings.AWeaponPath, "00", " R");
            InitLibrary(ref AHumEffect, Settings.AHumEffectPath, "00");

            //Archer
            InitLibrary(ref ARArmours, Settings.ARArmourPath, "00");
            InitLibrary(ref ARHair, Settings.ARHairPath, "00");
            InitLibrary(ref ARWeapons, Settings.ARWeaponPath, "00");
            InitLibrary(ref ARWeaponsS, Settings.ARWeaponPath, "00", " S");
            InitLibrary(ref ARHumEffect, Settings.ARHumEffectPath, "00");

            //Other
            InitLibrary(ref Monsters, Settings.MonsterPath, "000");
            InitLibrary(ref Gates, Settings.GatePath, "00");
            InitLibrary(ref Flags, Settings.FlagPath, "00");
            InitLibrary(ref Siege, Settings.SiegePath, "00");
            InitLibrary(ref NPCs, Settings.NPCPath, "00");
            InitLibrary(ref Mounts, Settings.MountPath, "00");
            InitLibrary(ref Fishing, Settings.FishingPath, "00");
            InitLibrary(ref Pets, Settings.PetsPath, "00");
            InitLibrary(ref Transform, Settings.TransformPath, "00");
            InitLibrary(ref TransformMounts, Settings.TransformMountsPath, "00");
            InitLibrary(ref TransformEffect, Settings.TransformEffectPath, "00");
            InitLibrary(ref TransformWeaponEffect, Settings.TransformWeaponEffectPath, "00");

            #region Maplibs
            //wemade mir2 (allowed from 0-99)
            MapLibs[0] = new MLibrary(Settings.DataPath + "Map\\WemadeMir2\\Tiles");
            MapLibs[1] = new MLibrary(Settings.DataPath + "Map\\WemadeMir2\\Smtiles");
            MapLibs[2] = new MLibrary(Settings.DataPath + "Map\\WemadeMir2\\Objects");
            for (int i = 2; i < 28; i++)
            {
                MapLibs[i + 1] = new MLibrary(Settings.DataPath + "Map\\WemadeMir2\\Objects" + i.ToString());
            }
            MapLibs[90] = new MLibrary(Settings.DataPath + "Map\\WemadeMir2\\Objects_32bit");

            //shanda mir2 (allowed from 100-199)
            MapLibs[100] = new MLibrary(Settings.DataPath + "Map\\ShandaMir2\\Tiles");
            for (int i = 1; i < 10; i++)
            {
                MapLibs[100 + i] = new MLibrary(Settings.DataPath + "Map\\ShandaMir2\\Tiles" + (i + 1));
            }
            MapLibs[110] = new MLibrary(Settings.DataPath + "Map\\ShandaMir2\\SmTiles");
            for (int i = 1; i < 10; i++)
            {
                MapLibs[110 + i] = new MLibrary(Settings.DataPath + "Map\\ShandaMir2\\SmTiles" + (i + 1));
            }
            MapLibs[120] = new MLibrary(Settings.DataPath + "Map\\ShandaMir2\\Objects");
            for (int i = 1; i < 31; i++)
            {
                MapLibs[120 + i] = new MLibrary(Settings.DataPath + "Map\\ShandaMir2\\Objects" + (i + 1));
            }
            MapLibs[190] = new MLibrary(Settings.DataPath + "Map\\ShandaMir2\\AniTiles1");
            //wemade mir3 (allowed from 200-299)
            string[] Mapstate = { "", "wood\\", "sand\\", "snow\\", "forest\\"};
            for (int i = 0; i < Mapstate.Length; i++)
            {
                MapLibs[200 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Tilesc");
                MapLibs[201 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Tiles30c");
                MapLibs[202 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Tiles5c");
                MapLibs[203 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Smtilesc");
                MapLibs[204 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Housesc");
                MapLibs[205 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Cliffsc");
                MapLibs[206 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Dungeonsc");
                MapLibs[207 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Innersc");
                MapLibs[208 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Furnituresc");
                MapLibs[209 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Wallsc");
                MapLibs[210 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "smObjectsc");
                MapLibs[211 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Animationsc");
                MapLibs[212 +(i*15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Object1c");
                MapLibs[213 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\WemadeMir3\\" + Mapstate[i] + "Object2c");
            }
            Mapstate = new string[] { "", "wood", "sand", "snow", "forest"};
            //shanda mir3 (allowed from 300-399)
            for (int i = 0; i < Mapstate.Length; i++)
            {
                MapLibs[300 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Tilesc" + Mapstate[i]);
                MapLibs[301 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Tiles30c" + Mapstate[i]);
                MapLibs[302 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Tiles5c" + Mapstate[i]);
                MapLibs[303 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Smtilesc" + Mapstate[i]);
                MapLibs[304 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Housesc" + Mapstate[i]);
                MapLibs[305 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Cliffsc" + Mapstate[i]);
                MapLibs[306 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Dungeonsc" + Mapstate[i]);
                MapLibs[307 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Innersc" + Mapstate[i]);
                MapLibs[308 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Furnituresc" + Mapstate[i]);
                MapLibs[309 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Wallsc" + Mapstate[i]);
                MapLibs[310 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "smObjectsc" + Mapstate[i]);
                MapLibs[311 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Animationsc" + Mapstate[i]);
                MapLibs[312 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Object1c" + Mapstate[i]);
                MapLibs[313 + (i * 15)] = new MLibrary(Settings.DataPath + "Map\\ShandaMir3\\" + "Object2c" + Mapstate[i]);
            }
            #endregion

            // 静态构造里不加载任何库：加载统一由 LoadAsync() 在 Program.Init 之后驱动，
            // 全程走 HTTP 异步，避免同步网络请求冻结主线程（渲染/输入/心跳）。

            // 把 MapLibs 的空槽填成占位库（_fileName=".Lib"，InitializeAsync 会跳过并标记 _failed，
            // 绘制时返回马赛克占位），避免绘制路径访问 null。不发起请求、不参与预加载。
            for (int i = 0; i < MapLibs.Length; i++)
                if (MapLibs[i] == null) MapLibs[i] = new MLibrary("");
        }

        /// <summary>
        /// 异步加载总入口：先并发加载首屏库（登录场景立即需要），再后台限流加载其余库。
        /// 由 Program.Init 以 fire-and-forget 方式启动，不阻塞启动流程。
        /// </summary>
        public static async Task LoadAsync()
        {
            BrowserResource.Log("[Mir] LoadAsync START");
            try
            {
                await LoadLibrariesAsync();   // 首屏库（登录/选角界面立即需要）
                // 其余库（地图/怪物/装备/特效等）不再启动时全量预加载：浏览器端 WASM 堆上限仅 2GB，
                // 全量加载数百个 .Lib 会让每个库的原始字节常驻内存（ParseIndex 用 MemoryStream 持有），
                // 并发下载的大数组会直接撑破堆造成 OOM。这些库已登记在静态字段中，绘制时 CheckImage
                // 会自动 InitializeAsync 按需加载，就绪后 LibraryLoaded 事件触发场景重绘（未就绪画马赛克）。
                Loaded = true;
            }
            catch (Exception ex)
            {
                BrowserResource.Log("[Mir] LoadAsync error: " + ex);
            }
        }

        /// <summary>并发加载队列中的库，用信号量限制并发数。</summary>
        private static async Task LoadQueuedAsync()
        {
            using var sem = new SemaphoreSlim(MaxConcurrentLoads);
            await Task.WhenAll(_queue.Select(async lib =>
            {
                if (lib == null) return;

                await sem.WaitAsync();
                try
                {
                    await lib.InitializeAsync();
                }
                finally
                {
                    sem.Release();
                    Progress++;
                }
            }));
        }

        // 浏览器端（WASM）没有本地文件系统：Directory.Exists / GetFiles 恒为 false / 空，
        // 会让数组长度退化成 1（NPCs[19]、CHumEffect[5] 等索引全部越界，即使文件其实都在资源服务器上）。
        // 因此本地目录不可用时改用固定容量：槽位先建好，但**只有被访问(绘制)的库才会真正发请求**。
        private const int BrowserLibraryCapacity = 256;

        static void InitLibrary(ref MLibrary[] library, string path, string toStringValue, string suffix = "")
        {
            int count;

            if (Directory.Exists(path))
            {
                var allFiles = Directory.GetFiles(path, "*" + suffix + MLibrary.Extention, SearchOption.TopDirectoryOnly)
                    .OrderBy(x => int.Parse(Regex.Match(x, @"\d+").Value));

                var lastFile = allFiles.Any() ? Path.GetFileName(allFiles.Last()) : "0";
                count = int.Parse(Regex.Match(lastFile, @"\d+").Value) + 1;
            }
            else
            {
                // 浏览器端：用固定容量，避免长度退化为 1 造成索引越界。
                count = BrowserLibraryCapacity;
            }

            library = new MLibrary[count];

            for (int i = 0; i < count; i++)
            {
                library[i] = new MLibrary(path + i.ToString(toStringValue) + suffix);
            }
        }

        /// <summary>首屏库（登录场景的背景 / UI 立即需要），优先并发加载、不等队列。</summary>
        static async Task LoadLibrariesAsync()
        {
            await Task.WhenAll(
                ChrSel.InitializeAsync(),
                Prguse.InitializeAsync(),
                Prguse2.InitializeAsync(),
                Prguse3.InitializeAsync(),
                UI_32bit.InitializeAsync(),
                Title.InitializeAsync());

            Progress += 6;
        }

        // [已弃用] 原全量预加载数百个库的逻辑：浏览器端 WASM 堆仅 2GB，全量加载会让每个库的原始字节
        // 常驻内存（ParseIndex 用 MemoryStream 持有）并撑破堆(OOM)。已改为绘制时按需加载（见 LoadAsync）。
        // 当前不再被调用，仅保留以免破坏引用；可整体删除（连同 _queue/LoadQueuedAsync/Count/Progress）。
        private static async Task LoadGameLibrariesAsync()
        {
            Count = MapLibs.Length + Monsters.Length + Gates.Length + Flags.Length + Siege.Length + NPCs.Length + CArmours.Length +
                CHair.Length + CWeapons.Length + CWeaponEffect.Length + AArmours.Length + AHair.Length + AWeaponsL.Length + AWeaponsR.Length +
                ARArmours.Length + ARHair.Length + ARWeapons.Length + ARWeaponsS.Length +
                CHumEffect.Length + AHumEffect.Length + ARHumEffect.Length + Mounts.Length + Fishing.Length + Pets.Length +
                Transform.Length + TransformMounts.Length + TransformEffect.Length + TransformWeaponEffect.Length + 19;

            _queue.Add(Dragon);
            Progress++;

            _queue.Add(BuffIcon);
            Progress++;

            _queue.Add(Help);
            Progress++;

            _queue.Add(MiniMap);
            Progress++;
            _queue.Add(MapLinkIcon);
            Progress++;

            _queue.Add(MagIcon);
            Progress++;
            _queue.Add(MagIcon2);
            Progress++;

            _queue.Add(Magic);
            Progress++;
            _queue.Add(Magic2);
            Progress++;
            _queue.Add(Magic3);
            Progress++;
            _queue.Add(MagicC);
            Progress++;

            _queue.Add(Effect);
            Progress++;

            _queue.Add(Weather);
            Progress++;

            _queue.Add(GuildSkill);
            Progress++;

            _queue.Add(Background);
            Progress++;

            _queue.Add(Deco);
            Progress++;

            _queue.Add(Items);
            Progress++;
            _queue.Add(StateItems);
            Progress++;
            _queue.Add(FloorItems);
            Progress++;
            _queue.Add(Items_Tooltip_32bit);
            Progress++;

            for (int i = 0; i < MapLibs.Length; i++)
            {
                if (MapLibs[i] == null)
                    MapLibs[i] = new MLibrary("");
                else
                    _queue.Add(MapLibs[i]);
                Progress++;
            }

            for (int i = 0; i < Monsters.Length; i++)
            {
                _queue.Add(Monsters[i]);
                Progress++;
            }

            for (int i = 0; i < Gates.Length; i++)
            {
                _queue.Add(Gates[i]);
                Progress++;
            }

            for (int i = 0; i < Flags.Length; i++)
            {
                _queue.Add(Flags[i]);
                Progress++;
            }

            for (int i = 0; i < Siege.Length; i++)
            {
                _queue.Add(Siege[i]);
                Progress++;
            }

            for (int i = 0; i < NPCs.Length; i++)
            {
                _queue.Add(NPCs[i]);
                Progress++;
            }


            for (int i = 0; i < CArmours.Length; i++)
            {
                _queue.Add(CArmours[i]);
                Progress++;
            }

            for (int i = 0; i < CHair.Length; i++)
            {
                _queue.Add(CHair[i]);
                Progress++;
            }

            for (int i = 0; i < CWeapons.Length; i++)
            {
                _queue.Add(CWeapons[i]);
                Progress++;
            }

			for (int i = 0; i < CWeaponEffect.Length; i++)
			{
				_queue.Add(CWeaponEffect[i]);
				Progress++;
			}

			for (int i = 0; i < AArmours.Length; i++)
            {
                _queue.Add(AArmours[i]);
                Progress++;
            }

            for (int i = 0; i < AHair.Length; i++)
            {
                _queue.Add(AHair[i]);
                Progress++;
            }

            for (int i = 0; i < AWeaponsL.Length; i++)
            {
                _queue.Add(AWeaponsL[i]);
                Progress++;
            }

            for (int i = 0; i < AWeaponsR.Length; i++)
            {
                _queue.Add(AWeaponsR[i]);
                Progress++;
            }

            for (int i = 0; i < ARArmours.Length; i++)
            {
                _queue.Add(ARArmours[i]);
                Progress++;
            }

            for (int i = 0; i < ARHair.Length; i++)
            {
                _queue.Add(ARHair[i]);
                Progress++;
            }

            for (int i = 0; i < ARWeapons.Length; i++)
            {
                _queue.Add(ARWeapons[i]);
                Progress++;
            }

            for (int i = 0; i < ARWeaponsS.Length; i++)
            {
                _queue.Add(ARWeaponsS[i]);
                Progress++;
            }

            for (int i = 0; i < CHumEffect.Length; i++)
            {
                _queue.Add(CHumEffect[i]);
                Progress++;
            }

            for (int i = 0; i < AHumEffect.Length; i++)
            {
                _queue.Add(AHumEffect[i]);
                Progress++;
            }

            for (int i = 0; i < ARHumEffect.Length; i++)
            {
                _queue.Add(ARHumEffect[i]);
                Progress++;
            }

            for (int i = 0; i < Mounts.Length; i++)
            {
                _queue.Add(Mounts[i]);
                Progress++;
            }


            for (int i = 0; i < Fishing.Length; i++)
            {
                _queue.Add(Fishing[i]);
                Progress++;
            }

            for (int i = 0; i < Pets.Length; i++)
            {
                _queue.Add(Pets[i]);
                Progress++;
            }

            for (int i = 0; i < Transform.Length; i++)
            {
                _queue.Add(Transform[i]);
                Progress++;
            }

            for (int i = 0; i < TransformEffect.Length; i++)
            {
                _queue.Add(TransformEffect[i]);
                Progress++;
            }

            for (int i = 0; i < TransformWeaponEffect.Length; i++)
            {
                _queue.Add(TransformWeaponEffect[i]);
                Progress++;
            }

            for (int i = 0; i < TransformMounts.Length; i++)
            {
                _queue.Add(TransformMounts[i]);
                Progress++;
            }

            // 统一并发限流加载（队列已在上面登记完毕）
            await LoadQueuedAsync();
            Loaded = true;
        }

    }

    public sealed class MLibrary
    {
        public const string Extention = ".Lib";
        public const int LibVersion = 3;

        private readonly string _fileName;

        private MImage[] _images;
        private FrameSet _frames;
        private int[] _indexList;
        private int _count;
        private bool _initialized;
        private bool _loading;   // 正在异步拉取字节（幂等，避免重复请求）
        private bool _loaded;    // 字节已就绪且索引已解析，绘制可用
        private bool _failed;    // 加载失败（缺资源/404），不再重试，避免每帧 404 风暴

        private BinaryReader _reader;
        private Stream _stream;

        public FrameSet Frames
        {
            get { return _frames; }
        }

        public MLibrary(string filename)
        {
            // 浏览器 WASM 下 System.IO.Path 不把 '\' 当作目录分隔符，导致 ChangeExtension 把
            // ".\Data\ChrSel" 误判为“扩展名”，整体替换成 ".Lib"（命中 InitializeAsync 的跳过分支，库永不加载 → 粉屏）。
            // 统一规范化为正斜杠相对路径并去掉开头的 "./" 前缀，ChangeExtension 即可正确得到 "Data/ChrSel.Lib"。
            if (!string.IsNullOrEmpty(filename))
            {
                filename = filename.Replace('\\', '/');
                while (filename.StartsWith("./")) filename = filename.Substring(2);
            }
            _fileName = Path.ChangeExtension(filename, Extention);
        }

        /// <summary>
        /// 异步加载：经 HTTP 拉取字节并解析索引表。幂等（加载中或已加载会直接返回）。
        /// 浏览器端资源在远程 HTTP 服务上，本地文件系统为空，因此不再用 File.Exists 判断存在性。
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_loaded || _loading || _failed) return;
            _loading = true;
            try
            {
                // 空文件名 / 仅扩展名的占位库（如 MapLibs 里未配置的空槽 new MLibrary("")，
                // 其 _fileName 为 ".Lib"）不发起请求，否则会每帧向资源服务器请求 "/.Lib" 造成 404 风暴。
                if (string.IsNullOrWhiteSpace(_fileName) || _fileName == Extention)
                {
                    _initialized = false;
                    _failed = true;
                    return;
                }

                BrowserResource.Log($"[Mir][lib] load {_fileName}");
                byte[] bytes = await BrowserResource.GetBytesAsync(_fileName);
                if (bytes == null || bytes.Length == 0)
                {
                    BrowserResource.Log($"[Mir][lib] empty: {_fileName}");
                    _initialized = false;
                    _failed = true;
                    return;
                }

                ParseIndex(bytes);
                _loaded = true;
                _initialized = true;

                // 输出图数与首图尺寸：用于确认地砖规格（Tiles 为 96x96 时底图按 2x2 绘制，
                // 48x48 时每格一张），排查"地板不显示"时非常关键。
                string sizeInfo = string.Empty;
                try
                {
                    if (_images != null && _images.Length > 0)
                    {
                        Size s0 = GetSize(0);
                        sizeInfo = $"，图数={_images.Length}，首图={s0.Width}x{s0.Height}";
                    }
                }
                catch { }

                BrowserResource.Log($"[Mir][lib] ok: {_fileName}（{bytes.Length} 字节{sizeInfo}）");
                Libraries.OnLibraryLoaded();
            }
            catch (Exception ex)
            {
                _initialized = false;
                _failed = true;
                BrowserResource.Log($"[Mir] 库加载失败 {_fileName}: {ex.Message}");
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>解析 .Lib 头部与索引表（与版本相关的结构）。</summary>
        private void ParseIndex(byte[] bytes)
        {
            _stream = new MemoryStream(bytes);
            _reader = new BinaryReader(_stream);
            int currentVersion = _reader.ReadInt32();
            if (currentVersion < 2)
            {
                BrowserResource.Log("Wrong lib version: " + _fileName + " expecting " + LibVersion + " found " + currentVersion);
                return;
            }
            _count = _reader.ReadInt32();

            int frameSeek = 0;
            if (currentVersion >= 3)
            {
                frameSeek = _reader.ReadInt32();
            }

            _images = new MImage[_count];
            _indexList = new int[_count];

            for (int i = 0; i < _count; i++)
                _indexList[i] = _reader.ReadInt32();

            if (currentVersion >= 3)
            {
                _stream.Seek(frameSeek, SeekOrigin.Begin);

                var frameCount = _reader.ReadInt32();

                if (frameCount > 0)
                {
                    _frames = new FrameSet();
                    for (int i = 0; i < frameCount; i++)
                    {
                        _frames.Add((MirAction)_reader.ReadByte(), new Frame(_reader));
                    }
                }
            }
        }

        private static long _lastMissingLogTime;
        private static int _missingImageCount;

        // 限频输出"图索引不存在"，用于判断"很多格子画不出来"是资源版本不匹配还是单纯缺图。
        private void LogMissingImage(int index)
        {
            _missingImageCount++;
            if (CMain.Time <= _lastMissingLogTime) return;

            _lastMissingLogTime = CMain.Time + 3000;
            MirEngine.BrowserResource.Log("[Img] 图不存在(不画): " + _fileName +
                " index=" + index + " 图片数=" + (_images != null ? _images.Length : 0) +
                " 累计=" + _missingImageCount);
        }

        // 缺失贴图占位：上传一次马赛克纹理，所有“无图”的绘制都复用它（避免粉色/黑块）。
        private const int MosaicHandle = -999999;
        private static MImage _mosaicImage;
        private static MImage MosaicImage
        {
            get
            {
                if (_mosaicImage == null)
                {
                    // 国服传奇风格：未加载区域是很暗的**细密小方格**（接近黑），不刺眼也不抢画面。
                    int s = 32;
                    byte[] rgba = new byte[s * s * 4];
                    for (int y = 0; y < s; y++)
                        for (int x = 0; x < s; x++)
                        {
                            bool a = ((x / 4) + (y / 4)) % 2 == 0;   // 4px 小格，铺在 48px 的地图格上呈细马赛克
                            int i = (y * s + x) * 4;
                            if (a) { rgba[i] = 46; rgba[i + 1] = 46; rgba[i + 2] = 46; rgba[i + 3] = 255; }
                            else { rgba[i] = 22; rgba[i + 1] = 22; rgba[i + 2] = 22; rgba[i + 3] = 255; }
                        }
                    BrowserCanvas.UploadImage(MosaicHandle, rgba, s, s);
                    _mosaicImage = new MImage { Width = (short)s, Height = (short)s, Image = new Texture(MosaicHandle, s, s), TextureValid = true };
                }
                return _mosaicImage;
            }
        }

        /// <summary>
        /// 返回可用于绘制的图像：
        /// - MosaicImage = 尚未就绪（正在按需异步加载）/ 加载失败 / 索引越界 / 图像损坏，
        ///   统一画马赛克占位，避免地图格子与物件在资源下载完成前是一大片空洞；
        ///   库加载完成后 LibraryLoaded 会触发场景重绘，自动替换为真实贴图。
        /// - 正常 MImage = 已就绪。
        /// 坚决不在绘制路径做同步网络请求（会冻结主线程/触发心跳超时）。
        /// </summary>
        public MImage CheckImage(int index)
        {
            if (!_loaded)
            {
                // 加载失败：直接不画，避免"加载完还残留马赛克"。
                if (_failed) return null;
                if (!_loading) _ = InitializeAsync();
                // 真正"正在加载"：用马赛克占位，加载完成后自动换成真实贴图。
                return MosaicImage;
            }

            if (_images == null) return null;

            // index < 0（常见为 -1）是 Mir2 里"该层没有图"的正常标记（如 MiddleImage-1），
            // 不算缺失，直接静默跳过，否则会把正常情况刷成海量日志。
            if (index < 0) return null;

            if (index >= _images.Length)
            {
                // 库已就绪但图号超出范围（资源版本不匹配 / 图缺失）：不画，不留马赛克。
                LogMissingImage(index);
                return null;
            }

            if (_images[index] == null)
            {
                _stream.Position = _indexList[index];
                _images[index] = new MImage(_reader);
            }

            MImage mi = _images[index];
            if (!mi.TextureValid)
            {
                if (mi.Width == 0 || mi.Height == 0)
                    return MosaicImage;
                _stream.Seek(_indexList[index] + 17, SeekOrigin.Begin);
                mi.CreateTexture(_reader);
            }

            return mi;
        }

        public Point GetOffSet(int index)
        {
            if (!_loaded) return Point.Empty;

            if (_images == null || index < 0 || index >= _images.Length)
                return Point.Empty;

            if (_images[index] == null)
            {
                _stream.Seek(_indexList[index], SeekOrigin.Begin);
                _images[index] = new MImage(_reader);
            }

            return new Point(_images[index].X, _images[index].Y);
        }
        public Size GetSize(int index)
        {
            if (!_loaded) return Size.Empty;
            if (_images == null || index < 0 || index >= _images.Length)
                return Size.Empty;

            if (_images[index] == null)
            {
                _stream.Seek(_indexList[index], SeekOrigin.Begin);
                _images[index] = new MImage(_reader);
            }

            return new Size(_images[index].Width, _images[index].Height);
        }
        public Size GetTrueSize(int index)
        {
            if (!_loaded) return Size.Empty;

            if (_images == null || index < 0 || index >= _images.Length)
                return Size.Empty;

            if (_images[index] == null)
            {
                _stream.Position = _indexList[index];
                _images[index] = new MImage(_reader);
            }
            MImage mi = _images[index];
            if (mi.TrueSize.IsEmpty)
            {
                if (!mi.TextureValid)
                {
                    if ((mi.Width == 0) || (mi.Height == 0))
                        return Size.Empty;

                    _stream.Seek(_indexList[index] + 17, SeekOrigin.Begin);
                    mi.CreateTexture(_reader);
                }
                return mi.GetTrueSize();
            }
            return mi.TrueSize;
        }

        public void Draw(int index, int x, int y)
        {
            if (x >= Settings.ScreenWidth || y >= Settings.ScreenHeight)
                return;

            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (x + mi.Width < 0 || y + mi.Height < 0)
                return;


            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)x, (float)y, 0.0F), Color.White);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Point point, Color colour, bool offSet = false)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void Draw(int index, Point point, Color colour, bool offSet, float opacity)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            DXManager.DrawOpaque(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour, opacity); 

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DrawBlend(int index, Point point, Color colour, bool offSet = false, float rate = 1)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            bool oldBlend = DXManager.Blending;
            DXManager.SetBlend(true, rate);

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            DXManager.SetBlend(oldBlend);
            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Rectangle section, Point point, Color colour, bool offSet)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);


            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            if (section.Right > mi.Width)
                section.Width -= section.Right - mi.Width;

            if (section.Bottom > mi.Height)
                section.Height -= section.Bottom - mi.Height;

            DXManager.Draw(mi.Image, section, new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Rectangle section, Point point, Color colour, float opacity)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;


            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            if (section.Right > mi.Width)
                section.Width -= section.Right - mi.Width;

            if (section.Bottom > mi.Height)
                section.Height -= section.Bottom - mi.Height;

            DXManager.DrawOpaque(mi.Image, section, new Vector3((float)point.X, (float)point.Y, 0.0F), colour, opacity); 

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Point point, Size size, Color colour)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + size.Width < 0 || point.Y + size.Height < 0)
                return;

            float scaleX = (float)size.Width / mi.Width;
            float scaleY = (float)size.Height / mi.Height;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new RectangleF(point.X, point.Y, size.Width, size.Height), Color.White);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DrawTinted(int index, Point point, Color colour, Color Tint, bool offSet = false)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            if (mi.HasMask)
            {
                DXManager.Draw(mi.MaskImage, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), Tint);
            }

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DrawUp(int index, int x, int y)
        {
            if (x >= Settings.ScreenWidth)
                return;

            MImage mi = CheckImage(index);
            if (mi == null)
                return;
            y -= mi.Height;
            if (y >= Settings.ScreenHeight)
                return;
            if (x + mi.Width < 0 || y + mi.Height < 0)
                return;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3(x, y, 0.0F), Color.White);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void DrawUpBlend(int index, Point point)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            point.Y -= mi.Height;


            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            bool oldBlend = DXManager.Blending;
            DXManager.SetBlend(true, 1);

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), Color.White);

            DXManager.SetBlend(oldBlend);
            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public bool VisiblePixel(int index, Point point, bool accuate)
        {
            MImage mi = CheckImage(index);
            if (mi == null || mi.Image == null || !mi.TextureValid || mi.Data == null)
                return false;

            if (accuate)
                return mi.VisiblePixel(point);

            int accuracy = 2;

            for (int x = -accuracy; x <= accuracy; x++)
                for (int y = -accuracy; y <= accuracy; y++)
                    if (mi.VisiblePixel(new Point(point.X + x, point.Y + y)))
                        return true;

            return false;
        }
    }

    public sealed class MImage
    {
        public short Width, Height, X, Y, ShadowX, ShadowY;
        public byte Shadow;
        public int Length;

        public bool TextureValid;
        public Texture Image;
        //layer 2:
        public short MaskWidth, MaskHeight, MaskX, MaskY;
        public int MaskLength;

        public Texture MaskImage;
        public Boolean HasMask;

        public long CleanTime;
        public Size TrueSize;

        public byte[] Data;
        private static int _imageKey = 1000000;

        public MImage(BinaryReader reader)
        {
            //read layer 1
            Width = reader.ReadInt16();
            Height = reader.ReadInt16();
            X = reader.ReadInt16();
            Y = reader.ReadInt16();
            ShadowX = reader.ReadInt16();
            ShadowY = reader.ReadInt16();
            Shadow = reader.ReadByte();
            Length = reader.ReadInt32();

            //check if there's a second layer and read it
            HasMask = ((Shadow >> 7) == 1) ? true : false;
            if (HasMask)
            {
                reader.ReadBytes(Length);
                MaskWidth = reader.ReadInt16();
                MaskHeight = reader.ReadInt16();
                MaskX = reader.ReadInt16();
                MaskY = reader.ReadInt16();
                MaskLength = reader.ReadInt32();
            }
        }

        public MImage() { }

        public void CreateTexture(BinaryReader reader)
        {
            int w = Width;
            int h = Height;

            byte[] raw = DecompressImage(reader.ReadBytes(Length));
            byte[] rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                rgba[i * 4] = raw[i * 4 + 2];
                rgba[i * 4 + 1] = raw[i * 4 + 1];
                rgba[i * 4 + 2] = raw[i * 4];
                rgba[i * 4 + 3] = raw[i * 4 + 3];
            }
            int id = System.Threading.Interlocked.Increment(ref _imageKey);
            BrowserCanvas.UploadImage(id, rgba, w, h);
            Image = new Texture(id, w, h);
            Data = raw;

            if (HasMask)
            {
                reader.ReadBytes(12);
                byte[] mraw = DecompressImage(reader.ReadBytes(MaskLength));
                byte[] mrgba = new byte[w * h * 4];
                for (int i = 0; i < w * h; i++)
                {
                    mrgba[i * 4] = mraw[i * 4 + 2];
                    mrgba[i * 4 + 1] = mraw[i * 4 + 1];
                    mrgba[i * 4 + 2] = mraw[i * 4];
                    mrgba[i * 4 + 3] = mraw[i * 4 + 3];
                }
                int mid = System.Threading.Interlocked.Increment(ref _imageKey);
                BrowserCanvas.UploadImage(mid, mrgba, w, h);
                MaskImage = new Texture(mid, w, h);
            }

            DXManager.TextureList.Add(this);
            TextureValid = true;

            CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DisposeTexture()
        {
            DXManager.TextureList.Remove(this);

            if (Image != null && !Image.Disposed)
            {
                Image.Dispose();
            }

            if (MaskImage != null && !MaskImage.Disposed)
            {
                MaskImage.Dispose();
            }

            TextureValid = false;
            Image = null;
            MaskImage = null;
            Data = null;
        }

        public bool VisiblePixel(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height)
                return false;

            int w = Width;

            bool result = false;
            if (Data != null)
            {
                int x = p.X;
                int y = p.Y;
                
                int index = (y * (w << 2)) + (x << 2) + 3;
                
                byte col = Data[index];

                if (col == 0) return false;
                else return true;
            }
            return result;
        }

        public Size GetTrueSize()
        {
            if (TrueSize != Size.Empty) return TrueSize;

            int l = 0, t = 0, r = Width, b = Height;

            bool visible = false;
            for (int x = 0; x < r; x++)
            {
                for (int y = 0; y < b; y++)
                {
                    if (!VisiblePixel(new Point(x, y))) continue;

                    visible = true;
                    break;
                }

                if (!visible) continue;

                l = x;
                break;
            }

            visible = false;
            for (int y = 0; y < b; y++)
            {
                for (int x = l; x < r; x++)
                {
                    if (!VisiblePixel(new Point(x, y))) continue;

                    visible = true;
                    break;

                }
                if (!visible) continue;

                t = y;
                break;
            }

            visible = false;
            for (int x = r - 1; x >= l; x--)
            {
                for (int y = 0; y < b; y++)
                {
                    if (!VisiblePixel(new Point(x, y))) continue;

                    visible = true;
                    break;
                }

                if (!visible) continue;

                r = x + 1;
                break;
            }

            visible = false;
            for (int y = b - 1; y >= t; y--)
            {
                for (int x = l; x < r; x++)
                {
                    if (!VisiblePixel(new Point(x, y))) continue;

                    visible = true;
                    break;

                }
                if (!visible) continue;

                b = y + 1;
                break;
            }

            TrueSize = Rectangle.FromLTRB(l, t, r, b).Size;

            return TrueSize;
        }

        private static byte[] DecompressImage(byte[] image)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(image), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

    }
}
