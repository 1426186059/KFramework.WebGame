namespace KFramework.MonoGame
{
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

        /// <summary>
        /// 通过指定的 <see cref="ContentManager"/> 从 URL 下载音频并加载
        /// （同步阻塞下载；解码仍异步进行，同 <see cref="FromBytes"/>）。
        /// 复用 <paramref name="mContentManager"/> 的下载/缓存链路（<c>LoadBytesAsync</c>），
        /// 走引擎统一的下载调度与 Cache Storage 缓存，不另建 HttpClient。
        /// MIME 按扩展名推断：.wav→audio/wav，.mp3→audio/mpeg，.ogg→audio/ogg，
        /// .m4a→audio/mp4，其余回退 application/octet-stream。
        /// 若需非阻塞加载，请自行 <c>await mContentManager.LoadBytesAsync(...)</c> 后调用 <see cref="FromBytes"/>。
        /// </summary>
        /// <param name="mContentManager">用于下载的 <see cref="ContentManager"/> 实例（如 <c>ContentManager.Default</c>）。</param>
        /// <param name="url">音频地址（绝对 URL 会被原样透传；相对路径则按 ContentManager 的 root 解析）。</param>
        public static SoundEffect FromURL(
            string url, 
            ContentManager mContentManager = null)
        {
            if(mContentManager == null)
            {
                mContentManager = ContentManager.Default;
            }

            byte[] data = mContentManager.LoadBytesAsync(url).GetAwaiter().GetResult();
            return FromBytes(data, MimeFromUrl(url));
        }

        private static string MimeFromUrl(string url)
        {
            int dot = url.LastIndexOf('.');
            string ext = dot >= 0 ? url.Substring(dot).ToLowerInvariant() : string.Empty;
            return ext switch
            {
                ".wav" => "audio/wav",
                ".mp3" => "audio/mpeg",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                _ => "application/octet-stream",
            };
        }

        internal int Handle => _handle;

        /// <summary>一次性播放。未解码或缓冲缺失时返回 false。</summary>
        public bool Play() => Play(1f, 1f, 0f);

        /// <summary>底层音频缓冲是否已在浏览器侧解码完成（实时查询，兼容 <see cref="FromBytes"/> 的异步解码）。</summary>
        private bool IsReady => _loaded || JSBind_Audio.IsLoaded(_handle);

        public bool Play(float volume, float pitch, float pan)
        {
            // 不再静默返回：解码未完成时丢弃播放是"声音听不见"的常见原因，必须能定位
            if (!IsReady)
            {
                Console.WriteLine($"[SoundEffect] 播放被丢弃：音频尚未解码完成 (handle={_handle})，" +
                                  $"请用 LoadAsync 等待解码，或稍后重试");
                return false;
            }

            int instance = JSBind_Audio.CreateInstance(_handle);
            if (instance == 0)
            {
                Console.WriteLine($"[SoundEffect] 播放失败：创建实例失败 (handle={_handle})，音频可能未解码");
                return false;
            }

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
