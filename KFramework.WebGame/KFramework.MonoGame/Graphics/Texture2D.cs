using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 2D 纹理（对应底层的一张 WebGL 纹理）。图集里的某个子图通过「单张整页纹理 + source rect」的方式绘制
    /// （照官方 MonoGame），同一张图集页能被 SpriteBatch 合并进同一个 draw call。
    /// 本类为 partial：公开 API 对齐 MonoGame 的 Texture2D，WebGL2 平台实现见 Texture2D.Web.cs。
    /// </summary>
    public partial class Texture2D : Texture
    {
        /// <summary>表面类型（照 MonoGame 的 Texture2D.SurfaceType）。</summary>
        internal protected enum SurfaceType
        {
            Texture,
            RenderTarget,
            SwapChainRenderTarget,
        }

        /// <summary>底层 WebGL 纹理对象（照 MonoGame 平台层的 gl 纹理句柄，存放在 .Web.cs 的 PlatformConstruct 中创建）。</summary>
        internal JSObject Handle = default;

        /// <summary>是否负责释放底层纹理。</summary>
        internal readonly bool OwnsHandle;

        /// <summary>是否为 GPU 压缩纹理（ASTC/BC7/DXT 等）；压缩纹理为不可变数据，不支持 SetData 局部更新。</summary>
        internal readonly bool IsCompressed;

        internal int width;
        internal int height;
        internal int ArraySize;

        internal float TexelWidth { get; private set; }
        internal float TexelHeight { get; private set; }

        /// <summary>底层 WebGL 纹理尺寸（整图时等于 Width/Height，保留以兼容 SpriteBatch 的 UV 计算）。</summary>
        public int TextureWidth => width;
        public int TextureHeight => height;

        /// <summary>纹理像素尺寸对应的矩形（整图时为 0,0,Width,Height，照 MonoGame）。</summary>
        public Rectangle Bounds => new Rectangle(0, 0, width, height);

        /// <summary>
        /// 创建未初始化的 <see cref="Texture2D"/>（默认 RGBA8、不生成 mipmap，照 MonoGame）。
        /// 数据须随后通过 <see cref="SetData{T}(T[])"/> 上传。
        /// </summary>
        public Texture2D(GraphicsDevice graphicsDevice, int width, int height)
            : this(graphicsDevice, width, height, false, SurfaceFormat.Color) { }

        /// <summary>创建未初始化的 <see cref="Texture2D"/>，可指定 mipmap 与像素格式（照 MonoGame）。</summary>
        public Texture2D(GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format)
            : this(graphicsDevice, width, height, mipmap, format, SurfaceType.Texture, false, 1) { }

        /// <summary>创建未初始化的 <see cref="Texture2D"/>，可指定纹理数组大小（照 MonoGame；当前 WebGL 后端仅支持 arraySize = 1）。</summary>
        public Texture2D(GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format, int arraySize)
            : this(graphicsDevice, width, height, mipmap, format, SurfaceType.Texture, false, arraySize) { }

        internal Texture2D(GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format, SurfaceType type)
            : this(graphicsDevice, width, height, mipmap, format, type, false, 1) { }

        /// <summary>真正的构造入口（照 MonoGame 的 protected 构造），负责校验、赋值并交由平台层 PlatformConstruct 创建 GL 纹理。</summary>
        protected Texture2D(GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format, SurfaceType type, bool shared, int arraySize)
        {
            if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice), "graphicsDevice 不能为空。");
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "纹理宽度必须大于零。");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "纹理高度必须大于零。");
            if (arraySize > 1) throw new ArgumentException("当前 WebGL 后端不支持纹理数组（arraySize 必须 <= 1）。", nameof(arraySize));

            this.graphicsDevice = graphicsDevice;
            this.width = width;
            this.height = height;
            this.TexelWidth = 1f / (float)width;
            this.TexelHeight = 1f / (float)height;

            Format = format;
            LevelCount = mipmap ? CalculateMipLevels(width, height) : 1;
            ArraySize = arraySize;
            IsCompressed = format.IsCompressed();
            OwnsHandle = true;

            // Swap chain 渲染目标的纹理由外部赋值，这里跳过 GL 创建。
            if (type == SurfaceType.SwapChainRenderTarget) return;

            PlatformConstruct(width, height, mipmap, format, type, shared);
        }

        /// <summary>纹理像素宽度（照 MonoGame）。</summary>
        public int Width => width;

        /// <summary>纹理像素高度（照 MonoGame）。</summary>
        public int Height => height;

        #region SetData（照 MonoGame 的 4 个重载）

        /// <summary>把数据复制到指定 mip 层级、数组切片与区域（照 MonoGame）。</summary>
        public void SetData<T>(int level, int arraySlice, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            Rectangle checkedRect;
            ValidateParams(level, arraySlice, rect, data, startIndex, elementCount, out checkedRect);
            PlatformSetData(level, arraySlice, checkedRect, data, startIndex, elementCount);
        }

        /// <summary>把数据复制到指定 mip 层级与区域（照 MonoGame）。</summary>
        public void SetData<T>(int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            Rectangle checkedRect;
            ValidateParams(level, 0, rect, data, startIndex, elementCount, out checkedRect);
            if (rect.HasValue)
                PlatformSetData(level, 0, checkedRect, data, startIndex, elementCount);
            else
                PlatformSetData(level, data, startIndex, elementCount);
        }

        /// <summary>把整层数据复制到纹理（照 MonoGame）。</summary>
        public void SetData<T>(T[] data, int startIndex, int elementCount) where T : struct
        {
            Rectangle checkedRect;
            ValidateParams(0, 0, null, data, startIndex, elementCount, out checkedRect);
            PlatformSetData(0, data, startIndex, elementCount);
        }

        /// <summary>把整层数据复制到纹理（照 MonoGame）。</summary>
        public void SetData<T>(T[] data) where T : struct
        {
            Rectangle checkedRect;
            ValidateParams(0, 0, null, data, 0, data.Length, out checkedRect);
            PlatformSetData(0, data, 0, data.Length);
        }

        #endregion

        #region GetData（照 MonoGame 的 4 个重载；WebGL 后端不支持 GPU 读回）

        /// <summary>把纹理数据读入数组（照 MonoGame）。当前 WebGL 后端不支持，调用会抛 NotSupportedException。</summary>
        public void GetData<T>(int level, int arraySlice, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            Rectangle checkedRect;
            ValidateParams(level, arraySlice, rect, data, startIndex, elementCount, out checkedRect);
            PlatformGetData(level, arraySlice, checkedRect, data, startIndex, elementCount);
        }

        /// <summary>把纹理数据读入数组（照 MonoGame）。</summary>
        public void GetData<T>(int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : struct
            => GetData(level, 0, rect, data, startIndex, elementCount);

        /// <summary>把纹理数据读入数组（照 MonoGame）。</summary>
        public void GetData<T>(T[] data, int startIndex, int elementCount) where T : struct
            => GetData(0, null, data, startIndex, elementCount);

        /// <summary>把纹理数据读入数组（照 MonoGame）。</summary>
        public void GetData<T>(T[] data) where T : struct
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            GetData(0, null, data, 0, data.Length);
        }

        #endregion

        #region 兼容旧调用方的便捷重载（非 MonoGame API，保留以支持 SpriteFont 等局部更新）

        /// <summary>上传 RGBA8 像素数据到整张纹理。</summary>
        public void SetData(byte[] rgba) => SetData(rgba, 0, 0, width, height);

        /// <summary>更新纹理的局部区域（用于动态字形图集等）。</summary>
        public void SetData(byte[] rgba, int x, int y, int w, int h)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            SetData(0, new Rectangle(x, y, w, h), rgba, 0, w * h * Format.GetSize());
        }

        #endregion

        /// <summary>计算给定宽高下的 mip 层级数（照 MonoGame 的 Texture2D.CalculateMipLevels）。</summary>
        internal static int CalculateMipLevels(int width, int height)
        {
            int levels = 1;
            while ((width | height) >> levels != 0) levels++;
            return levels;
        }

        private void ValidateParams<T>(int level, int arraySlice, Rectangle? rect, T[] data,
            int startIndex, int elementCount, out Rectangle checkedRect) where T : struct
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (startIndex < 0 || startIndex >= data.Length)
                throw new ArgumentException("startIndex 必须 >= 0 且小于 data.Length。", nameof(startIndex));
            if (data.Length < startIndex + elementCount)
                throw new ArgumentException("data 数组太小。", nameof(data));
            CommonValidations<T>(level, arraySlice, rect, elementCount, out checkedRect);
        }

        private void CommonValidations<T>(int level, int arraySlice, Rectangle? rect,
             int elementCount, out Rectangle checkedRect) where T : struct
        {
            var textureBounds = new Rectangle(0, 0, Math.Max(width >> level, 1), Math.Max(height >> level, 1));
            checkedRect = rect ?? textureBounds;
            if (level < 0 || level >= LevelCount) throw new ArgumentException("level 必须小于纹理的 mip 层级数。", nameof(level));
            if (arraySlice > 0) throw new ArgumentException("当前 WebGL 后端不支持纹理数组。", nameof(arraySlice));
            if (arraySlice < 0 || arraySlice >= ArraySize) throw new ArgumentException("arraySlice 必须 >= 0 且小于 ArraySize。", nameof(arraySlice));
            if (!textureBounds.Contains(checkedRect) || checkedRect.Width <= 0 || checkedRect.Height <= 0)
                throw new ArgumentException("Rectangle 必须在纹理范围内。", nameof(rect));

            int tSize = Marshal.SizeOf<T>();
            int fSize = Format.GetSize();
            if (tSize > fSize || fSize % tSize != 0)
                throw new ArgumentException("类型 T 的大小与该纹理格式不兼容。", nameof(T));

            int dataByteSize = Format.IsCompressed()
                ? SurfaceFormatGL.GetExpectedCompressedBytes(Format, checkedRect.Width, checkedRect.Height)
                : checkedRect.Width * checkedRect.Height * fSize;

            if (dataByteSize <= 0)
                throw new InvalidOperationException("无法计算该纹理格式的预期字节数。");
            if (elementCount * tSize != dataByteSize)
                throw new ArgumentException(
                    $"elementCount 大小不正确：elementCount * sizeof(T) = {elementCount * tSize}，但数据应为 {dataByteSize} 字节。",
                    nameof(elementCount));
        }

        /// <summary>把任意 blittable 结构体数组按字节 reinterpret 为 byte[]，供 WebGL 上传（照 MonoGame 的 data.ToBytes）。</summary>
        private static byte[] ToByteArray<T>(T[] data, int startIndex, int elementCount) where T : struct
        {
            ReadOnlySpan<T> slice = new ReadOnlySpan<T>(data, startIndex, elementCount);
            return MemoryMarshal.AsBytes(slice).ToArray();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && OwnsHandle) JSBind_GL.DeleteTexture(Handle);
        }
    }
}
