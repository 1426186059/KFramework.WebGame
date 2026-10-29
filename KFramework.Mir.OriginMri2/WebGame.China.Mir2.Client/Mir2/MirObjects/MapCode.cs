using System.IO;
using WebGame.Mir2.MonoGame.Client;
using MirEngine;

namespace Client.MirObjects
{
    public class CellInfo
    {
        public short BackIndex;
        public int BackImage;
        public short MiddleIndex;
        public int MiddleImage;
        public short FrontIndex;
        public int FrontImage;

        public byte DoorIndex;
        public byte DoorOffset;

        public byte FrontAnimationFrame;
        public byte FrontAnimationTick;

        public byte MiddleAnimationFrame;
        public byte MiddleAnimationTick;

        public short TileAnimationImage;
        public short TileAnimationOffset;
        public byte  TileAnimationFrames;

        public byte Light;
        public byte Unknown;
        public List<MapObject> CellObjects;

        public bool FishingCell;

        public void AddObject(MapObject ob)
        {
            if (CellObjects == null) CellObjects = new List<MapObject>();

            CellObjects.Insert(0, ob);
            Sort();
        }
        public void RemoveObject(MapObject ob)
        {
            if (CellObjects == null) return;

            CellObjects.Remove(ob);

            if (CellObjects.Count == 0) CellObjects = null;
            else Sort();
        }
        public MapObject FindObject(uint ObjectID)
        {
            return CellObjects.Find(
                delegate(MapObject mo)
            {
                return mo.ObjectID == ObjectID;
            });
        }
        public void DrawObjects()
        {
            if (CellObjects == null) return;

            for (int i = 0; i < CellObjects.Count; i++)
            {
                if (!CellObjects[i].Dead)
                {
                    CellObjects[i].Draw();
                    continue;
                }

                if(CellObjects[i].Race == ObjectType.Monster)
                {
                    switch(((MonsterObject)CellObjects[i]).BaseImage)
                    {
                        case Monster.PalaceWallLeft:
                        case Monster.PalaceWall1:
                        case Monster.PalaceWall2:
                        case Monster.SSabukWall1:
                        case Monster.SSabukWall2:
                        case Monster.SSabukWall3:
                        case Monster.HellLord:
                            CellObjects[i].Draw();
                            break;
                        default:
                            continue;
                    }
                }
            }
        }

        public void DrawDeadObjects()
        {
            if (CellObjects == null) return;
            for (int i = 0; i < CellObjects.Count; i++)
            {
                if (!CellObjects[i].Dead) continue;

                if (CellObjects[i].Race == ObjectType.Monster)
                {
                    switch (((MonsterObject)CellObjects[i]).BaseImage)
                    {
                        case Monster.PalaceWallLeft:
                        case Monster.PalaceWall1:
                        case Monster.PalaceWall2:
                        case Monster.SSabukWall1:
                        case Monster.SSabukWall2:
                        case Monster.SSabukWall3:
                        case Monster.HellLord:
                            continue;
                    }
                }

                CellObjects[i].Draw();
            }
        }

        public void Sort()
        {
            CellObjects.Sort(delegate(MapObject ob1, MapObject ob2)
            {
                if (ob1.Race == ObjectType.Item && ob2.Race != ObjectType.Item)
                    return -1;
                if (ob2.Race == ObjectType.Item && ob1.Race != ObjectType.Item)
                    return 1;
                if (ob1.Race == ObjectType.Spell && ob2.Race != ObjectType.Spell)
                    return -1;
                if (ob2.Race == ObjectType.Spell && ob1.Race != ObjectType.Spell)
                    return 1;

                int i = ob2.Dead.CompareTo(ob1.Dead);
                return i == 0 ? ob1.ObjectID.CompareTo(ob2.ObjectID) : i;
            });
        }
    }

    /// <summary>
    /// 原始传奇地图读取：不再解析 .map 二进制瓦片索引，改为从 CDN 下载服务端同款合并
    /// map10.25341.1.dat，按 FileName(id) 抽取碰撞网格；瓦片视觉由 MapControl 通过
    /// assets/resource/zipmap/{img}/{index}.jpg 真实瓦片拼出（与官方客户端一致）。
    /// 解码逻辑见 Mir2MapSource（移植自服务端 OriginalMapSource/OriginalMapDecoder）。
    /// </summary>
    class MapReader
    {
        public string FileName;

        public MapReader(string fileName) { FileName = fileName; }

        public int Width, Height;
        public int MapImg;                  // 瓦片组(img)，供 MapControl 从 CDN 拉 zipmap 真实瓦片
        public int PixelWidth, PixelHeight; // 配置像素宽高（计算 zipmap 瓦片网格用）
        public CellInfo[,] MapCells;

        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            // 保留新资源系统的远程开关（UI 图集仍可能走远程镜像），但不依赖 .map
            NewResConfig.RemoteLibEnabled = true;
            if (!int.TryParse(Path.GetFileNameWithoutExtension(FileName), out int id))
                id = 0;

            if (id <= 0)
            {
                BuildBlank();
                return;
            }

            var (data, entry) = await Mir2MapDataLoader.LoadAsync(id, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) return;

            if (data == null || data.CellFlags == null || data.Cols <= 0 || data.Rows <= 0)
            {
                KFramework.MonoGame.PrintTool.Log($"[Map] 地图 {id} 在合并 dat 中未找到，回退空白地图");
                BuildBlank();
                return;
            }

            Width = data.Cols;
            Height = data.Rows;
            MapImg = entry?.Img ?? id;
            PixelWidth = entry?.Width ?? (data.Cols * 48);
            PixelHeight = entry?.Height ?? (data.Rows * 32);
            NewResConfig.CurrentMapName = id.ToString();

            // 首次进图卡顿采样：整张地图的 CellInfo 网格在这里同步构建（await 之后、主线程），
            // 大图可达百万格，是“进 GameScene 卡 3 秒”的嫌疑点之一。
            var t0 = DateTime.Now;
            MapCells = new CellInfo[Width, Height];
            int idx = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var c = new CellInfo();
                    // 与服务端一致：bit3(0x08)=1 表示不可走(墙) → 标记 FrontImage 0x8000（客户端 EmptyCell 据此判阻挡）
                    if ((data.CellFlags[idx++] & 8) != 0) c.FrontImage = 0x8000;
                    MapCells[x, y] = c;
                }
            CMain.LogStall($"[Map] 构建 CellInfo 网格 {Width}x{Height}={Width * Height} 格", (DateTime.Now - t0).TotalMilliseconds);

            KFramework.MonoGame.PrintTool.Log($"[Map] 地图就绪: id={id} 名称={entry?.Name} img={MapImg} " +
                $"尺寸={Width}x{Height} 像素={PixelWidth}x{PixelHeight} 时刻={DateTime.Now:HH:mm:ss.fff}");
        }

        private void BuildBlank()
        {
            Width = 1000;
            Height = 1000;
            MapCells = new CellInfo[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    MapCells[x, y] = new CellInfo();
        }
    }
}
