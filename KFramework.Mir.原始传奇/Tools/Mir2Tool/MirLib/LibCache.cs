using SkiaSharp;

namespace Mir.Lib
{
    /// <summary>
    /// 按索引「单张」从 .Lib 取图并直接存成 PNG 的缓存。
    /// 只在真正缺图时才抽取，不整库导出；
    /// 并且复用已打开的 FileStream（否则每次都要重读一遍数 MB 的索引表）。
    /// 用于磁盘紧张场景：只落地地图真正引用到的那几张图。
    /// </summary>
    public sealed class LibCache : IDisposable
    {
        private sealed class Handle : IDisposable
        {
            public FileStream Stream = null!;
            public BinaryReader Reader = null!;
            public int[] IndexList = Array.Empty<int>();
            public int Count;

            public void Dispose()
            {
                try { Reader?.Dispose(); } catch { }
                try { Stream?.Dispose(); } catch { }
            }
        }

        private readonly Dictionary<string, Handle?> _handles = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        /// <summary>已按需抽取的图片张数</summary>
        public int ExtractedCount { get; private set; }
        /// <summary>尝试过但 Lib 中不存在/失败的张数</summary>
        public int MissedCount { get; private set; }

        /// <summary>
        /// 从 libPath 中抽取第 index 张图，保存为 outputPng。
        /// 已存在返回 true（但不计入抽取数）。
        /// </summary>
        public bool TryExtract(string libPath, int index, string outputPng)
        {
            if (_disposed || index < 0) return false;

            if (File.Exists(outputPng)) return true;

            var h = GetHandle(libPath);
            if (h == null || index >= h.Count) { MissedCount++; return false; }

            try
            {
                h.Stream.Position = h.IndexList[index];
                var mi = new MLibraryV2.MImage(h.Reader);
                mi.CreateTextureFromBytes();

                if (mi.Image == null) { MissedCount++; return false; }

                string? dir = Path.GetDirectoryName(outputPng);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                SkiaBitmaps.SavePng(mi.Image, outputPng);
                mi.Image.Dispose();
                mi.Image = null;

                ExtractedCount++;
                return true;
            }
            catch
            {
                MissedCount++;
                return false;
            }
        }

        /// <summary>
        /// 只取图像尺寸：MImage 头部前两个 Int16 就是 Width/Height，
        /// 不需要解压像素、也不需要落地成文件。
        /// </summary>
        public bool TryGetImageSize(string libPath, int index, out int width, out int height)
        {
            width = height = 0;
            if (_disposed || index < 0) return false;

            var h = GetHandle(libPath);
            if (h == null || index >= h.Count) return false;

            try
            {
                h.Stream.Position = h.IndexList[index];
                short w = h.Reader.ReadInt16();
                short hh = h.Reader.ReadInt16();
                width = w;
                height = hh;
                return w > 0 && hh > 0;
            }
            catch
            {
                return false;
            }
        }

        private Handle? GetHandle(string libPath)
        {
            if (_handles.TryGetValue(libPath, out var h)) return h;

            Handle? handle = null;
            try
            {
                if (!File.Exists(libPath)) { _handles[libPath] = null; return null; }

                var fs = new FileStream(libPath, FileMode.Open, FileAccess.Read);
                var reader = new BinaryReader(fs);

                int version = reader.ReadInt32();
                if (version < 2)
                {
                    reader.Dispose();
                    fs.Dispose();
                    _handles[libPath] = null;
                    return null;
                }

                int count = reader.ReadInt32();
                if (version >= 3) reader.ReadInt32(); // frameSeek

                var indexList = new int[count];
                for (int i = 0; i < count; i++) indexList[i] = reader.ReadInt32();

                handle = new Handle { Stream = fs, Reader = reader, IndexList = indexList, Count = count };
            }
            catch
            {
                handle = null;
            }

            _handles[libPath] = handle;
            return handle;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var h in _handles.Values)
                h?.Dispose();
            _handles.Clear();
        }
    }
}
