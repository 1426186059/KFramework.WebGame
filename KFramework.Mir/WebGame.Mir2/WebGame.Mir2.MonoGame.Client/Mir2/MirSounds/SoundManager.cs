using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.MirSounds.Libraries;
using KFramework.MonoGame;
using MirEngine;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirSounds
{
    /// <summary>
    /// 音效管理器。
    /// 浏览器端没有 NAudio/WaveOut 这类原生音频后端，整套播放下沉到 WebAudio
    /// （MirEngine.BrowserAudio），这里只负责：索引 -> 文件路径、音量、循环/延迟与停止。
    /// </summary>
    public static class SoundManager
    {
        // 索引 -> 文件名，来自 Sound/SoundList.lst（浏览器端经资源服务器读取）
        private static readonly Dictionary<int, string> _indexList = SoundList.Indexes;
        private static readonly List<KeyValuePair<long, int>> _delayList = new List<KeyValuePair<long, int>>();
        private static readonly Dictionary<int, LoopProvider> _loopingSounds = new Dictionary<int, LoopProvider>();
        private static LoopProvider _music;

        private static int _vol;
        private static int _musicVol;

        public static readonly List<string> SupportedFileTypes = new List<string>
        {
            ".wav",
            ".mp3"
        };

        public static ISoundLibrary Music => _music;

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

        public static async Task CreateAsync()
        {
            // 索引 -> 文件名 映射表。原桌面端在窗体初始化时加载，浏览器端此前从没被调用过，
            // 导致所有 PlaySound(索引) 都解析出错误文件名 -> 全程静音。
            await SoundList.LoadSoundListAsync().ConfigureAwait(false);

            // 音频后端改为 KFramework.MonoGame（WebAudio）；实际解锁需首次用户手势，在 ConfigureInput 里触发。
            AudioMaster.Unlock();
        }

        /// <summary>
        /// 把声音索引解析成可请求的资源路径（如 ".\Sound\1.wav"）。
        /// 表里没有的索引按原版命名规则推测文件名。
        /// </summary>
        public static string ResolveSoundFile(int index)
        {
            if (!_indexList.TryGetValue(index, out string fileName) || string.IsNullOrWhiteSpace(fileName))
            {
                fileName = index > 20000
                    ? string.Format("M{0:0}-{1:0}", (index - 20000) / 10, index % 10)
                    : string.Format("{0:000}-{1:0}", index / 10, index % 10);

                _indexList[index] = fileName;
            }

            fileName = fileName.Trim();

            // SoundList.lst 里都带扩展名；推测名不带，默认补 .wav（该客户端音效全是 wav）
            if (Path.GetExtension(fileName).Length == 0) fileName += ".wav";

            return Path.Combine(Settings.SoundPath, fileName);
        }

        public static void PlaySound(int index, bool loop = false, int delay = 0)
        {
            if (delay > 0)
            {
                _delayList.Add(new KeyValuePair<long, int>(CMain.Time + delay, index));
                return;
            }

            string file = ResolveSoundFile(index);

            if (!loop)
            {
                _ = PlayFileAsync(file, Vol, false);
            }
            else
            {
                var sound = LoopProvider.TryCreate(index, file, Vol, true);
                if (sound != null)
                {
                    _loopingSounds[index] = sound;
                }
            }
        }

        public static void StopSound(int index)
        {
            if (_loopingSounds.TryGetValue(index, out LoopProvider sound))
            {
                _loopingSounds.Remove(index);
                sound.Dispose();
            }
        }

        public static void PlayMusic(int index, bool loop = false)
        {
            StopMusic();

            _music = LoopProvider.TryCreate(index, ResolveSoundFile(index), MusicVol, loop);
        }

        public static void StopMusic()
        {
            _music?.Dispose();
            _music = null;
        }

        public static void ProcessDelayedSounds()
        {
            if (_delayList.Count == 0) return;

            var sounds = _delayList.Where(x => x.Key <= CMain.Time).ToList();

            foreach (var sound in sounds)
            {
                _delayList.Remove(sound);

                PlaySound(sound.Value);
            }
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
            _delayList.Clear();
        }

        // 音效字节内存缓存：同一音效只从网络 / Cache Storage 取一次，之后直接内存命中。
        // 之前每次播放都重新 GetBytesAsync：Cache 读取实测 0.2~7.8s，而且会占满浏览器单域名
        // 约 6 个并发连接，把 Map/*.map（十几 MB）挤到排队 —— 表现为地图加载不完、画不全。
        private static readonly Dictionary<string, byte[]> _soundBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _soundMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _soundLock = new object();

        /// <summary>
        /// 取音效字节（带内存缓存）：命中内存则零 I/O；取不到（404/空）也记一笔，避免反复发起注定失败的请求。
        /// </summary>
        internal static async Task<byte[]> GetSoundBytesAsync(string file)
        {
            string url = BrowserResource.ResolveUrl(file);

            lock (_soundLock)
            {
                if (_soundMissing.Contains(url)) return null;
                if (_soundBytes.TryGetValue(url, out byte[] cached)) return cached;
            }

            byte[] bytes = await BrowserResource.GetBytesAsync(url);
            if (bytes == null || bytes.Length == 0)
            {
                lock (_soundLock) _soundMissing.Add(url);
                return null;
            }

            lock (_soundLock) _soundBytes[url] = bytes;
            return bytes;
        }

        // 浏览器端用 KFramework.MonoGame 的 SoundEffect：先按资源 URL 异步取字节（fetch），
        // 再按扩展名 mime 构造 SoundEffect 播放（循环用 SoundEffectInstance）。
        private static async Task PlayFileAsync(string file, int vol, bool loop)
        {
            try
            {
                byte[] bytes = await GetSoundBytesAsync(file);
                if (bytes == null || bytes.Length == 0) return;
                string mime = file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? "audio/mpeg" : "audio/wav";
                var se = await SoundEffect.LoadAsync(bytes, mime);
                if (se == null) return;
                if (loop)
                {
                    var inst = se.CreateInstance();
                    inst.IsLooped = true;
                    inst.Volume = vol / 100f;
                    inst.Play();
                }
                else
                {
                    se.Play(vol / 100f, 1f, 0f);
                }
            }
            catch (Exception ex)
            {
                if (Settings.LogErrors) CMain.SaveError(ex.ToString());
            }
        }
    }
}
