using System;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    public partial class Texture2D : Texture
    {
        /// <summary>
        /// 平台层：创建底层纹理并设置初始采样参数（照 MonoGame 的 PlatformConstruct）。
        /// <para>具体 WebGL 调用已迁到 <c>WebGl20Backend</c>（见 Graphics/Backend/WebGl20Backend.cs），
        /// 这里只做转发，使纹理的平台操作与渲染后端解耦。</para>
        /// </summary>
        private void PlatformConstruct(int width, int height, bool mipmap, SurfaceFormat format, SurfaceType type, bool shared)
            => graphicsDevice.Backend.CreateTexture(this, width, height, mipmap, format, type);

        /// <summary>平台层：整层上传（照 MonoGame 的 PlatformSetData）。字节转换留在本类，上传交给后端；同时保留 CPU 副本。</summary>
        private void PlatformSetData<T>(int level, T[] data, int startIndex, int elementCount) where T : struct
        {
            byte[] bytes = ToByteArray(data, startIndex, elementCount);
            StoreCpuData(level, new Rectangle(0, 0, width, height), bytes);
            graphicsDevice.Backend.SetTextureData(this, level, bytes);
        }

        /// <summary>平台层：区域上传（照 MonoGame 的 PlatformSetData）。整层区域走 TexImage2D，子区域走 TexSubImage2D；压缩纹理仅支持整块上传。同时并入 CPU 副本。</summary>
        private void PlatformSetData<T>(int level, int arraySlice, Rectangle rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            byte[] bytes = ToByteArray(data, startIndex, elementCount);
            StoreCpuData(level, rect, bytes);
            graphicsDevice.Backend.SetTextureData(this, level, rect, bytes);
        }

        /// <summary>把上传像素字节按区域并入 mip0 的 CPU 副本（满层则整体替换）。仅 level 0 保留副本；
        /// readable 为 false 时不保留任何副本；readable 为 true 时构造已禁止压缩格式，故此处无需处理压缩分支。</summary>
        private void StoreCpuData(int level, Rectangle rect, byte[] bytes)
        {
            if (!_readable) return;
            if (level != 0) return;
            int fSize = Format.GetSize();
            int fullSize = width * height * fSize;
            if (_cpuData == null || _cpuData.Length != fullSize) _cpuData = new byte[fullSize];
            int rowBytes = rect.Width * fSize;
            int destPitch = width * fSize;
            for (int row = 0; row < rect.Height; row++)
            {
                int srcOff = row * rowBytes;
                int dstOff = ((rect.Y + row) * destPitch) + rect.X * fSize;
                Buffer.BlockCopy(bytes, srcOff, _cpuData, dstOff, rowBytes);
            }
        }

        /// <summary>
        /// 平台层：从 CPU 副本读回像素（照 MonoGame 的 PlatformGetData）。
        /// <para>显存对 CPU 不可直接访问，本框架改为在 SetData 上传时保留一份 CPU 副本（等价于 Unity 的 Read/Write Enabled），
        /// 因此 GetData 是同步的、与后端无关（WebGL / WebGPU 通用），也无需昂贵的 readPixels 拷贝。
        /// 仅支持 mip0；压缩纹理不支持 CPU 可读写（创建时即禁止 readable），故 GetData 不会命中压缩路径。</para>
        /// </summary>
        private void PlatformGetData<T>(int level, int arraySlice, Rectangle rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            if (!_readable)
                throw new InvalidOperationException(
                    "此纹理未开启可读写（创建 Texture2D 时 readable 未设为 true），GetData 不可用；请创建时传入 readable: true 以在 CPU 侧保留像素副本。");
            if (_cpuData == null)
                throw new InvalidOperationException(
                    "此纹理尚未通过 SetData 上传过数据，CPU 侧无像素副本，GetData 不可用；如需读回请先通过 SetData 上传。");
            if (level != 0)
                throw new NotSupportedException("当前 GetData 仅支持读取 CPU 副本的 mip0，更高层级未保留副本。");

            int fSize = Format.GetSize();
            int tSize = Marshal.SizeOf<T>();

            byte[] tmp = new byte[elementCount * tSize];
            int rowBytes = rect.Width * fSize;
            int srcPitch = width * fSize;
            for (int row = 0; row < rect.Height; row++)
            {
                int srcOff = ((rect.Y + row) * srcPitch) + rect.X * fSize;
                int dstOff = row * rowBytes;
                Buffer.BlockCopy(_cpuData, srcOff, tmp, dstOff, rowBytes);
            }
            Buffer.BlockCopy(tmp, 0, data, startIndex * tSize, elementCount * tSize);
        }
    }
}
