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

        public static readonly List<string> SupportedFileTypes = new List<string>
        {
            ".mp3"
        };

        public static LoopProvider Music => _music;

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

        /// <summary>
        /// 播放原始传奇音效（CDN：assets/resource/audio/{soundId}.mp3）。
        /// soundId 来自 5config.skillEffects[].sound（如灵魂火符=20025），与经典 Mir2 的索引体系无关，
        /// 不要走 ResolveSoundFile（那会把 id 解析成本地 wav 文件名导致无声）。
        /// </summary>
        public static void PlayOriginSound(int soundId, bool loop = false)
        {
            if (soundId <= 0) return;
            _ = PlayOriginFileAsync(soundId, loop);
        }

        private static async Task PlayOriginFileAsync(int soundId, bool loop)
        {
            try
            {
                byte[] bytes = await Mir2OriginResource.GetOriginAudioBytesAsync(soundId).ConfigureAwait(false);
                if (bytes == null || bytes.Length == 0) return;
                var se = await SoundEffect.LoadAsync(bytes).ConfigureAwait(false);
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
        }
    }
}
