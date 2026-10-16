using Client.MirObjects;
using SlimDX;
using System.IO.Compression;
using WebGame.Mir2.MonoGame.Client;
using Frame = Client.MirObjects.Frame;

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

            // 静态构造里不加载任何库：加载统一由 LoadAsync() 在 CMain.Init 之后驱动，
            // 全程走 HTTP 异步，避免同步网络请求冻结主线程（渲染/输入/心跳）。

            // 把 MapLibs 的空槽填成占位库（_fileName=".Lib"，InitializeAsync 会跳过并标记 _failed，
            // 绘制时返回马赛克占位），避免绘制路径访问 null。不发起请求、不参与预加载。
            for (int i = 0; i < MapLibs.Length; i++)
                if (MapLibs[i] == null) MapLibs[i] = new MLibrary("");
        }

        /// <summary>
        /// 异步加载总入口：先并发加载首屏库（登录场景立即需要），再后台限流加载其余库。
        /// 由 CMain.Init 以 fire-and-forget 方式启动，不阻塞启动流程。
        /// </summary>
        public static async Task LoadAsync()
        {
            KFramework.MonoGame.PrintTool.Log("[Mir] LoadAsync START");
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
                KFramework.MonoGame.PrintTool.Log("[Mir] LoadAsync error: " + ex);
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
        //
        // 容量必须 >= 服务端可能下发的最大编号，否则裸索引会越界并终止主循环。
        // 之前写死 256 不够：MonsterObject 里 Libraries.Monsters[(ushort)BaseImage]，
        // 服务端下发的怪物 ID 超过 256 就 IndexOutOfRange（原版按「最后一个文件编号+1」算，
        // 磁盘上有多少就是多少，不会撞这个上限）。
        // 槽位本身只是空 MLibrary 对象（不加载字节），调大不会多发请求，开销可忽略。
        private const int BrowserLibraryCapacity = 1024;

        static void InitLibrary(ref MLibrary[] library, string path, string toStringValue, string suffix = "")
        {
            int count;

            // 浏览器端（WASM）无本地文件系统：Directory.Exists / GetFiles 恒为 false / 空，
            // 不再走本地目录探测，统一用固定容量，避免长度退化为 1 造成索引越界（NPCs[19]、CHumEffect[5] 等）。
            count = BrowserLibraryCapacity;

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
}
