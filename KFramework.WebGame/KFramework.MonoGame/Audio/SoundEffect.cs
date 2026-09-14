using System.Threading;
using KFramework.JSBind;

namespace KFramework
{

    /// <summary>
    /// 音效资源（对齐 MonoGame 的 <c>SoundEffect</c>）。
    /// 支持从 wav/mp3/ogg 等编码字节创建，浏览器侧经 decodeAudioData 解码成 AudioBuffer。
    /// 解码是异步的，<see cref="LoadAsync"/> 会等待就绪；<see cref="Play"/> 在解码未完成时直接忽略。
    /// 跨语言调用统一走 <see cref="JSBind_Audio"/>。
    /// </summary>
    public sealed class SoundEffect : IDisposable
    {
        private static int _nextHandle = 1;

        private readonly int _handle;
        private bool _loaded;

        private SoundEffect(int handle) => _handle = handle;

        /// <summary>解码完成后的时长；未解码时为 <see cref="TimeSpan.Zero"/>。</summary>
        public TimeSpan Duration { get; private set; }

        /// <summary>底层音频缓冲是否已在浏览器侧解码完成。</summary>
        public bool IsLoaded => _loaded;

        /// <summary>提交编码字节给浏览器解码，立即返回（解码在后台进行）。</summary>
        /// <param name="mime">MIME 类型，如 "audio/wav"、"audio/mpeg"、"audio/ogg"。</param>
        public static SoundEffect FromBytes(byte[] data, string mime)
        {
            var effect = new SoundEffect(Interlocked.Increment(ref _nextHandle));
            JSBind_Audio.LoadAudio(effect._handle, data, mime);
            return effect;
        }

        /// <summary>提交字节并等待浏览器解码完成。</summary>
        public static async Task<SoundEffect> LoadAsync(byte[] data, string mime)
        {
            var effect = FromBytes(data, mime);
            while (!JSBind_Audio.IsLoaded(effect._handle))
                await Task.Yield();

            effect._loaded = true;
            effect.Duration = TimeSpan.FromSeconds(JSBind_Audio.GetDuration(effect._handle));
            return effect;
        }

        internal int Handle => _handle;

        /// <summary>一次性播放。未解码或缓冲缺失时返回 false。</summary>
        public bool Play() => Play(1f, 1f, 0f);

        /// <summary>底层音频缓冲是否已在浏览器侧解码完成（实时查询，兼容 <see cref="FromBytes"/> 的异步解码）。</summary>
        private bool IsReady => _loaded || JSBind_Audio.IsLoaded(_handle);

        public bool Play(float volume, float pitch, float pan)
        {
            if (!IsReady) return false;

            int instance = JSBind_Audio.CreateInstance(_handle);
            if (instance == 0) return false;

            JSBind_Audio.PlayInstance(instance, volume, pitch, pan, loop: false);
            return true;   // 实例由 JS 侧 onended 自动释放
        }

        /// <summary>创建受控播放实例（可暂停/循环/调音量）。</summary>
        public SoundEffectInstance CreateInstance()
        {
            int instance = IsReady ? JSBind_Audio.CreateInstance(_handle) : 0;
            return new SoundEffectInstance(instance);
        }

        public void Dispose() => JSBind_Audio.ReleaseBuffer(_handle);
    }
}
