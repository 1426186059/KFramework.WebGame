using System.Collections.Generic;
using Client.MirSounds.Libraries;
using KFramework.MonoGame;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirSounds
{
    /// <summary>
    /// 音效管理器。经典 Mir2 的本地 wav 音效已停用（5080 资源服务器上取不到，全是 404），
    /// 只保留原始传奇 CDN 音效（PlayOriginSound / PlayOriginMusic，audio/{id}.mp3）。
    /// 旧 PlaySound(int)/PlayMusic(int) 保留签名兼容大量调用点，但不再发起任何请求。
    /// </summary>
    public static class SoundManager
    {
        private static readonly Dictionary<int, LoopProvider> _loopingSounds = new Dictionary<int, LoopProvider>();
        private static LoopProvider _music;

        private static int _vol;
        private static int _musicVol;

        public static int Vol
        {
            get { return _vol; }
            set
            {
                if (_vol == value) return;
                _vol = value;

                AdjustAllVolumes();
            }
        }

        public static int MusicVol
        {
            get { return _musicVol; }
            set
            {
                if (_musicVol == value) return;
                _musicVol = value;

                _music?.SetVolume(_musicVol);
            }
        }

        public static Task CreateAsync()
        {
            // 经典 SoundList.lst / wav 体系已停用，不再加载索引表；音频后端只需首次用户手势解锁。
            AudioMaster.Unlock();
            return Task.CompletedTask;
        }

        /// <summary>经典 Mir2 索引音效已停用：保留签名兼容调用点，不再发起任何 wav 请求。</summary>
        public static void PlaySound(int index, bool loop = false, int delay = 0)
        {
        }

        // 音效节流：进 GameScene 瞬间服务端会一口气下发大量对象包，同一音效（如命中音 6、攻击音 2）
        // 会在同一帧被触发几十次，全部叠加播放会非常刺耳。这里做两层限流：
        // 1) 同一 soundId 冷却 100ms；2) 不同音效之间最小间隔 30ms（循环音/BGM 不受限）。
        private const int SameSoundCooldownMs = 100;
        private const int GlobalSoundIntervalMs = 30;
        private static readonly Dictionary<int, long> _lastSoundTime = new Dictionary<int, long>();
        private static long _lastAnySoundTime;
        // 同一音效只需解码一次 SoundEffect：Play() 每次会新建实例，JS 侧 onended 自动释放，可安全复用。
        private static readonly Dictionary<int, SoundEffect> _soundEffects = new Dictionary<int, SoundEffect>();
        private static readonly Dictionary<int, Task<SoundEffect>> _soundEffectTasks = new Dictionary<int, Task<SoundEffect>>();
        private static readonly object _sfxLock = new object();

        /// <summary>
        /// 播放原始传奇音效（CDN：assets/resource/audio/{soundId}.mp3）。
        /// soundId 来自 5config.skillEffects[].sound（如灵魂火符=20025），与经典 Mir2 的索引体系无关，
        /// 不要走 ResolveSoundFile（那会把 id 解析成本地 wav 文件名导致无声）。
        /// </summary>
        public static void PlayOriginSound(int soundId, bool loop = false)
        {
            if (soundId <= 0) return;

            // 循环音效不走节流（数量极少，且需要持续播放）
            if (!loop)
            {
                long now = Environment.TickCount64;
                lock (_sfxLock)
                {
                    if (_lastSoundTime.TryGetValue(soundId, out long last) && now - last < SameSoundCooldownMs) return;
                    if (now - _lastAnySoundTime < GlobalSoundIntervalMs) return;
                    _lastSoundTime[soundId] = now;
                    _lastAnySoundTime = now;
                }
            }

            _ = PlayOriginFileAsync(soundId, loop);
        }

        private static Task<SoundEffect> GetOrLoadSoundEffectAsync(int soundId)
        {
            lock (_sfxLock)
            {
                if (_soundEffects.TryGetValue(soundId, out var cached)) return Task.FromResult(cached);
                if (_soundEffectTasks.TryGetValue(soundId, out var pending)) return pending;
            }

            Task<SoundEffect> task = LoadAndCacheSoundEffectAsync(soundId);
            lock (_sfxLock)
            {
                if (_soundEffectTasks.TryGetValue(soundId, out var existing)) return existing;
                _soundEffectTasks[soundId] = task;
            }
            return task;
        }

        private static async Task<SoundEffect> LoadAndCacheSoundEffectAsync(int soundId)
        {
            SoundEffect se = await LoadSoundEffectAsync(soundId).ConfigureAwait(false);
            lock (_sfxLock)
            {
                _soundEffectTasks.Remove(soundId);
                if (se != null) _soundEffects[soundId] = se;
            }
            return se;
        }

        private static async Task<SoundEffect> LoadSoundEffectAsync(int soundId)
        {
            try
            {
                byte[] bytes = await Mir2OriginResource.GetOriginAudioBytesAsync(soundId).ConfigureAwait(false);
                if (bytes == null || bytes.Length == 0) return null;
                return await SoundEffect.LoadAsync(bytes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
                return null;
            }
        }

        private static async Task PlayOriginFileAsync(int soundId, bool loop)
        {
            try
            {
                SoundEffect se = await GetOrLoadSoundEffectAsync(soundId).ConfigureAwait(false);
                if (se == null) return;
                if (loop)
                {
                    var inst = se.CreateInstance();
                    inst.IsLooped = true;
                    inst.Volume = Vol / 100f;
                    inst.Play();
                }
                else
                {
                    se.Play(Vol / 100f, 1f, 0f);
                }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }

        public static void StopSound(int index)
        {
        }

        /// <summary>经典 Mir2 索引音乐已停用：背景音乐统一走 PlayOriginMusic。</summary>
        public static void PlayMusic(int index, bool loop = false)
        {
        }

        /// <summary>
        /// 按地图 id 取原始传奇 BGM 的 fileId。
        /// 直接读 5config.soundnormal[type=3][地图id].file（配置驱动，不再硬编码）。
        /// 配置未加载 / 该地图无 BGM 返回 0（不播）。
        /// </summary>
        public static int GetOriginMapBgm(int mapIndex)
        {
            return ExcelTableMgr.TryGetMapBgm(mapIndex, out int fileId) ? fileId : 0;
        }

        /// <summary>
        /// 播放原始传奇背景音乐：fileId 来自 5config.soundnormal[type3][地图id].file，
        /// 走 CDN audio/{fileId}.mp3。若 fileId<=0（该地图无 BGM 配置）则停止当前音乐。
        /// </summary>
        public static void PlayOriginMusic(int fileId, bool loop = true)
        {
            StopMusic();
            if (fileId <= 0) return;
            _music = LoopProvider.TryCreateOrigin(fileId, MusicVol, loop);
        }

        public static void StopMusic()
        {
            _music?.Dispose();
            _music = null;
        }

        public static void ProcessDelayedSounds()
        {
        }

        private static void AdjustAllVolumes()
        {
            foreach (LoopProvider sound in _loopingSounds.Values)
            {
                sound.SetVolume(Vol);
            }

            _music?.SetVolume(MusicVol);
        }

        public static void Dispose()
        {
            StopMusic();

            foreach (LoopProvider sound in _loopingSounds.Values) { sound.Dispose(); }
            _loopingSounds.Clear();

            lock (_sfxLock)
            {
                foreach (SoundEffect se in _soundEffects.Values) se.Dispose();
                _soundEffects.Clear();
                _soundEffectTasks.Clear();
                _lastSoundTime.Clear();
            }
        }
    }
}
