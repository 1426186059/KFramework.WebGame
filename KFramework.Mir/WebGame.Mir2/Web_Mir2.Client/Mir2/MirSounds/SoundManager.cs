using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.MirSounds.Libraries;
using MirEngine;

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

        public static void Create()
        {
            // 索引 -> 文件名 映射表。原桌面端在窗体初始化时加载，浏览器端此前从没被调用过，
            // 导致所有 PlaySound(索引) 都解析出错误文件名 -> 全程静音。
            SoundList.LoadSoundList();

            BrowserAudio.InitAudio();
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
                BrowserAudio.PlaySound(file, Vol, false);
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

            BrowserAudio.StopAllSounds();
        }
    }
}
