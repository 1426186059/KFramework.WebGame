using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 所有 GPU 资源的基类（照 MonoGame 的 GraphicsResource）。
    /// 持有所属 GraphicsDevice，提供 Name/Tag 与标准的 Dispose 模式。
    /// </summary>
    public abstract class GraphicsResource : IDisposable
    {
        internal GraphicsDevice? graphicsDevice;

        /// <summary>创建该资源的 GraphicsDevice（照 MonoGame）。</summary>
        public GraphicsDevice GraphicsDevice => graphicsDevice!;

        /// <summary>调试用名称（照 MonoGame 的 GraphicsResource.Name）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>用户自定义附加对象（照 MonoGame 的 GraphicsResource.Tag）。</summary>
        public object? Tag { get; set; }

        /// <summary>资源是否已释放（照 MonoGame 的 GraphicsResource.IsDisposed）。</summary>
        public bool IsDisposed { get; private set; }

        ~GraphicsResource() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            IsDisposed = true;
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }

        /// <summary>设备重置时调用；WebGL 后端无需重建资源，这里为空实现（保持与官方同名 API）。</summary>
        protected void GraphicsDeviceResetting() { }
    }
}
