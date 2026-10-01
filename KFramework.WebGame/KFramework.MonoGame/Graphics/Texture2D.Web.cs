using System;

namespace KFramework.MonoGame
{
    public partial class Texture2D : Texture
    {
        /// <summary>
        /// 平台层：创建底层 WebGL 纹理并设置初始采样参数（照 MonoGame 的 PlatformConstruct）。
        /// </summary>
        private void PlatformConstruct(int width, int height, bool mipmap, SurfaceFormat format, SurfaceType type, bool shared)
        {
            Handle = JSBind_WEBGL20.CreateTexture();
            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, Handle);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_S, JSBind_WEBGL20.CLAMP_TO_EDGE);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_T, JSBind_WEBGL20.CLAMP_TO_EDGE);

            // 压缩纹理为不可变 GPU 数据，过滤固定为 LINEAR；未压缩用 NEAREST（照原 CreateTexture 行为）。
            int filter = format.IsCompressed() ? JSBind_WEBGL20.LINEAR : JSBind_WEBGL20.NEAREST;
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MIN_FILTER, filter);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MAG_FILTER, filter);

            // 渲染目标（照 MonoGame 的 RenderTarget2D.PlatformConstruct）：内容由 GPU 绘制产生，
            // 这里只把存储分配出来（传 null 像素），否则 FBO 挂的是一张没有存储的不完整纹理。
            if (type == SurfaceType.RenderTarget)
            {
                if (format.IsCompressed())
                    throw new ArgumentException("渲染目标不支持压缩格式。", nameof(format));

                JSBind_WEBGL20.TexImage2DStorage(JSBind_WEBGL20.TEXTURE_2D, 0, JSBind_WEBGL20.RGBA8,
                    width, height, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE);
            }
        }

        /// <summary>平台层：整层上传（照 MonoGame 的 PlatformSetData）。</summary>
        private void PlatformSetData<T>(int level, T[] data, int startIndex, int elementCount) where T : struct
        {
            byte[] bytes = ToByteArray(data, startIndex, elementCount);
            int levelWidth = Math.Max(width >> level, 1);
            int levelHeight = Math.Max(height >> level, 1);

            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, Handle);
            JSBind_WEBGL20.PixelStorei(JSBind_WEBGL20.UNPACK_ALIGNMENT, 1);

            if (Format.IsCompressed())
                JSBind_WEBGL20.CompressedTexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, (int)Format, levelWidth, levelHeight, 0, bytes.AsSpan());
            else
                JSBind_WEBGL20.TexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, JSBind_WEBGL20.RGBA8, levelWidth, levelHeight, 0, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE, bytes.AsSpan());
        }

        /// <summary>平台层：区域上传（照 MonoGame 的 PlatformSetData）。整层区域走 TexImage2D，子区域走 TexSubImage2D；压缩纹理仅支持整块上传。</summary>
        private void PlatformSetData<T>(int level, int arraySlice, Rectangle rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            byte[] bytes = ToByteArray(data, startIndex, elementCount);
            int levelWidth = Math.Max(width >> level, 1);
            int levelHeight = Math.Max(height >> level, 1);

            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, Handle);
            JSBind_WEBGL20.PixelStorei(JSBind_WEBGL20.UNPACK_ALIGNMENT, 1);

            if (Format.IsCompressed())
            {
                if (rect.X != 0 || rect.Y != 0 || rect.Width != levelWidth || rect.Height != levelHeight)
                    throw new InvalidOperationException("压缩纹理为不可变 GPU 数据，不支持 SetData 局部更新；如需更新请整张重建。");
                JSBind_WEBGL20.CompressedTexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, (int)Format, levelWidth, levelHeight, 0, bytes.AsSpan());
            }
            else if (rect.X == 0 && rect.Y == 0 && rect.Width == levelWidth && rect.Height == levelHeight)
                JSBind_WEBGL20.TexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, JSBind_WEBGL20.RGBA8, levelWidth, levelHeight, 0, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE, bytes.AsSpan());
            else
                JSBind_WEBGL20.TexSubImage2D(JSBind_WEBGL20.TEXTURE_2D, level, rect.X, rect.Y, rect.Width, rect.Height, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE, bytes.AsSpan());
        }

        /// <summary>平台层：从 GPU 读回（照 MonoGame 的 PlatformGetData；WebGL 后端不支持）。</summary>
        private void PlatformGetData<T>(int level, int arraySlice, Rectangle rect, T[] data, int startIndex, int elementCount) where T : struct
        {
            throw new NotSupportedException("WebGL 后端不支持从 GPU 读回纹理数据（GetData）。");
        }
    }
}
