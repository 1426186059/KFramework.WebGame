using Client.MirObjects;
using SlimDX;
using System.IO.Compression;
using WebGame.Mir2.MonoGame.Client;
using Frame = Client.MirObjects.Frame;

namespace Client.MirGraphics
{
    public sealed class MImage
    {
        public short Width, Height, X, Y, ShadowX, ShadowY;
        public byte Shadow;
        public int Length;

        public bool TextureValid;
        public KFramework.MonoGame.Texture2D Image;
        //layer 2:
        public short MaskWidth, MaskHeight, MaskX, MaskY;
        public int MaskLength;

        public KFramework.MonoGame.Texture2D MaskImage;
        public Boolean HasMask;

        public long CleanTime;
        public Size TrueSize;

        public byte[] Data;
        private static int _imageKey = 1000000;

        public MImage(BinaryReader reader)
        {
            try
            {
                // 防御：图库文件被截断时（索引表完整、但像素数据缺失），reader 读头部途中会
                // ReadInt16/ReadByte 越界抛 EndOfStreamException；该异常在每帧 DrawFloor->GetSize
                // 路径上无法被个别 try 捕获，会中断整帧绘制（表现为走着走着卡死/黑屏）。
                // 读前先确认剩余字节足够容纳最小头部(6*short + 1*byte + 1*int = 17 字节)。
                if (reader.BaseStream.Position + 17 > reader.BaseStream.Length)
                {
                    Width = Height = 0;
                    return;
                }

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
                    // 跳过第一层数据；限制读取量，避免 Length 异常时的超大数组分配。
                    long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
                    if (Length > 0 && Length <= remaining)
                        reader.ReadBytes(Length);
                    else
                        reader.ReadBytes((int)Math.Max(0, Math.Min(remaining, int.MaxValue)));

                    // 第二层头部(5*short = 10 字节)不足则不再读取，保留已读的 Width/Height。
                    if (reader.BaseStream.Position + 10 <= reader.BaseStream.Length)
                    {
                        MaskWidth = reader.ReadInt16();
                        MaskHeight = reader.ReadInt16();
                        MaskX = reader.ReadInt16();
                        MaskY = reader.ReadInt16();
                        MaskLength = reader.ReadInt32();
                    }
                }
            }
            catch (IOException)
            {
                // 截断/损坏：标记为 0 尺寸占位图，由 CheckImage(返回 MosaicImage)/CreateTexture
                // 的 0 尺寸守卫跳过绘制，不再中断渲染帧。
                Width = Height = 0;
            }
        }

        public MImage() { }

        public void CreateTexture(BinaryReader reader)
        {
            int w = Width;
            int h = Height;

            // 防御：库里偶发 0 尺寸（或负）占位图。直接跳过创建，避免 GDevice.CreateTexture
            // 抛 ArgumentOutOfRangeException；该异常会被 CMain.Loop 的 catch 吞掉，
            // 导致此后每帧只清黑屏、场景不再上屏（永久黑屏）。标记为已处理以防重入。
            if (w <= 0 || h <= 0)
            {
                TextureValid = true;
                return;
            }

            byte[] raw = DecompressImage(reader.ReadBytes(Length));
            // 数据长度不足（库损坏/尺寸不符）时同样跳过，避免越界与创建异常。
            if (raw == null || raw.Length < (long)w * h * 4)
            {
                TextureValid = true;
                return;
            }

            byte[] rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                rgba[i * 4] = raw[i * 4 + 2];
                rgba[i * 4 + 1] = raw[i * 4 + 1];
                rgba[i * 4 + 2] = raw[i * 4];
                rgba[i * 4 + 3] = raw[i * 4 + 3];
            }
            Image = DXManager.GDevice.CreateTexture(w, h, rgba);
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
                MaskImage = DXManager.GDevice.CreateTexture(w, h, mrgba);
            }

            DXManager.TextureList.Add(this);
            TextureValid = true;

            CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DisposeTexture()
        {
            DXManager.TextureList.Remove(this);

            if (Image != null)
            {
                Image.Dispose();
            }

            if (MaskImage != null)
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
