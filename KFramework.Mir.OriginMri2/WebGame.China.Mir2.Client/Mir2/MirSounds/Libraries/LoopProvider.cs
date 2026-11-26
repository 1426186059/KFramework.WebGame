using KFramework.MonoGame;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirSounds.Libraries
{
    /// <summary>
    /// 可循环 / 可停止的原始传奇 CDN 音效（背景音乐、持续吟唱、光环等）。
    /// 经典 Mir2 本地 wav 已停用，本类只走 audio/{fileId}.mp3。
    /// </summary>
    public class LoopProvider : IDisposable
    {
        public int Index { get; set; }
        public long ExpireTime { get; set; }

        private readonly bool _loop;
        private readonly int _originSoundId;

        private SoundEffect _se;
        private SoundEffectInstance _inst;
        private int _volume;
        private bool _disposed;

        /// <summary>
        /// 原始传奇背景音乐：fileId 来自 5config.soundnormal[type3][地图id].file，
        /// 通过 CDN audio/{fileId}.mp3 加载并循环。
        /// </summary>
        public static LoopProvider TryCreateOrigin(int fileId, int volume, bool loop)
        {
            if (fileId <= 0) return null;
            return new LoopProvider(fileId, volume, loop, fileId);
        }

        private LoopProvider(int index, int volume, bool loop, int originSoundId)
        {
            Index = index;
            _loop = loop;
            _originSoundId = originSoundId;

            _ = PlayAsync(volume);
        }

        private async Task PlayAsync(int volume)
        {
            try
            {
                _volume = volume;
                ExpireTime = CMain.Time + Settings.SoundCleanMinutes * 60 * 1000;

                if (_se != null) return;

                byte[] bytes = await Mir2OriginResource.GetOriginAudioBytesAsync(_originSoundId).ConfigureAwait(false);
                if (bytes == null || bytes.Length == 0) return;

                _se = await SoundEffect.LoadAsync(bytes);
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
