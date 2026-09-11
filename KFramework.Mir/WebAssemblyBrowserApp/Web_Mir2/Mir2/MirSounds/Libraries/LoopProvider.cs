using System;
using MirEngine;

namespace Client.MirSounds.Libraries
{
    /// <summary>
    /// 可循环 / 可停止的声音（背景音乐、持续吟唱、光环等）。
    /// 浏览器端由 WebAudio 的 AudioBufferSourceNode 承担循环，这里只持有它的句柄。
    /// </summary>
    internal class LoopProvider : ISoundLibrary, IDisposable
    {
        public int Index { get; set; }
        public long ExpireTime { get; set; }

        private readonly string _fileName;
        private readonly bool _loop;

        private int _audioId;
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

            Play(volume);
        }

        public bool IsPlaying()
        {
            return _audioId != 0;
        }

        public void Play(int volume)
        {
            _volume = volume;
            ExpireTime = CMain.Time + Settings.SoundCleanMinutes * 60 * 1000;

            if (_audioId != 0) return;

            _audioId = BrowserAudio.PlaySound(_fileName, _volume, _loop);
        }

        public void SetVolume(int vol)
        {
            _volume = vol;

            if (_audioId != 0) BrowserAudio.SetSoundVolume(_audioId, vol);
        }

        public void Stop()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_audioId != 0)
            {
                BrowserAudio.StopSound(_audioId);
                _audioId = 0;
            }
        }
    }
}
