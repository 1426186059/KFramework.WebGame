using System;
using System.Threading.Tasks;
using KFramework.MonoGame;
using MirEngine;

namespace Client.MirSounds.Libraries
{
    /// <summary>
    /// 可循环 / 可停止的声音（背景音乐、持续吟唱、光环等）。
    /// 浏览器端改用 KFramework.MonoGame 的 SoundEffect / SoundEffectInstance（底层 WebAudio），
    /// 这里持有 SoundEffect 与循环实例句柄。
    /// </summary>
    internal class LoopProvider : ISoundLibrary, IDisposable
    {
        public int Index { get; set; }
        public long ExpireTime { get; set; }

        private readonly string _fileName;
        private readonly bool _loop;

        private SoundEffect _se;
        private SoundEffectInstance _inst;
        private int _volume;
        private bool _disposed;

        /// <summary>
        /// fileName 需为 <see cref="SoundManager.ResolveSoundFile"/> 解析出的资源路径。
        /// </summary>
        public static LoopProvider TryCreate(int index, string fileName, int volume, bool loop)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            return new LoopProvider(index, fileName, volume, loop);
        }

        private LoopProvider(int index, string fileName, int volume, bool loop)
        {
            Index = index;
            _fileName = fileName;
            _loop = loop;

            _ = PlayAsync(volume);
        }

        private async Task PlayAsync(int volume)
        {
            try
            {
                _volume = volume;
                ExpireTime = CMain.Time + Settings.SoundCleanMinutes * 60 * 1000;

                if (_se != null) return;

                string url = BrowserResource.ResolveUrl(_fileName);
                byte[] bytes = await BrowserResource.GetBytesAsync(url);
                if (bytes == null || bytes.Length == 0) return;

                string mime = _fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? "audio/mpeg" : "audio/wav";
                _se = await SoundEffect.LoadAsync(bytes, mime);
                if (_se == null) return;

                if (_loop)
                {
                    _inst = _se.CreateInstance();
                    _inst.IsLooped = true;
                    _inst.Volume = _volume / 100f;
                    _inst.Play();
                }
                else
                {
                    _se.Play(_volume / 100f, 1f, 0f);
                }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

        public bool IsPlaying()
        {
            return _inst != null && _inst.State == SoundState.Playing;
        }

        public void Play(int volume)
        {
            _volume = volume;
            if (_se == null) { _ = PlayAsync(volume); return; }
            if (_loop)
            {
                if (_inst == null)
                {
                    _inst = _se.CreateInstance();
                    _inst.IsLooped = true;
                }
                _inst.Volume = _volume / 100f;
                _inst.Play();
            }
            else
            {
                _se.Play(_volume / 100f, 1f, 0f);
            }
        }

        public void SetVolume(int vol)
        {
            _volume = vol;
            if (_inst != null) _inst.Volume = vol / 100f;
        }

        public void Stop() => Dispose();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _inst?.Stop();
            _inst?.Dispose();
            _se?.Dispose();
            _inst = null;
            _se = null;
        }
    }
}
