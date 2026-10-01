using System;

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

        /// <summary>平台层：整层上传（照 MonoGame 的 PlatformSetData）。字节转换留在本类，上传交给后端。</summary>
        private void PlatformSetData<T>(int level, T[] data, int startIndex, int elementCount) where T : struct
        {
            byte[] bytes = ToByteArray(data, startIndex, elementCount);
            graphicsDevice.Backend.SetTextureData(this, level, bytes);
        }

        /// <summary>平台层：区域上传（照 MonoGame 的 PlatformSetData）。整层区域走 TexImage2D，子区域走 TexSubImage2D；压缩纹理仅支持整块上传。</summary>
        private void PlatformSetData<T>(int level, int arraySlice, Rectangle rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            byte[] bytes = ToByteArray(data, startIndex, elementCount);
            graphicsDevice.Backend.SetTextureData(this, level, rect, bytes);
        }

        /// <summary>平台层：从 GPU 读回（照 MonoGame 的 PlatformGetData；WebGL 后端不支持）。</summary>
        private void PlatformGetData<T>(int level, int arraySlice, Rectangle rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            throw new NotSupportedException("WebGL 后端不支持从 GPU 读回纹理数据（GetData）。");
        }
    }
}
