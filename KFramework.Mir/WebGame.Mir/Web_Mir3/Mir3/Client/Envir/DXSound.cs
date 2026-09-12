using Shared.Rendering;
using System;
using System.IO;

namespace Client.Envir
{
    /// <summary>
    /// WASM 版音效：不再使用 NAudio / SharpDX，改为把 .wav 路径交给 JS 端 Web Audio 播放。
    /// 公开行为与桌面版一致（Play/Stop/SetVolume/DisposeSoundBuffer/UpdateFlags），所有调用点无需改动。
    /// </summary>
    public sealed class DXSound
    {
        public string FileName { get; set; }

        public DateTime ExpireTime { get; set; }
        public bool Loop { get; set; }

        public SoundType SoundType { get; set; }

        public int Volume { get; set; }

        private int _activeId;

        public DXSound(string fileName, SoundType type)
        {
            FileName = fileName;
            SoundType = type;

            Volume = DXSoundManager.GetVolume(SoundType);
        }

        public void Play()
        {
            // 循环音效已在播放则跳过，避免重复触发
            if (Loop && _activeId != 0) return;

            string url = "MyRes/Sound/" + Path.GetFileName(FileName);
            _activeId = MirEngine.BrowserAudio.PlaySound(url, Volume, Loop);
        }

        public void Stop()
        {
            if (_activeId != 0)
            {
                MirEngine.BrowserAudio.StopSound(_activeId);
                _activeId = 0;
            }
        }

        public void DisposeSoundBuffer()
        {
            Stop();
            ExpireTime = DateTime.MinValue;
        }

        public void SetVolume()
        {
            Volume = DXSoundManager.GetVolume(SoundType);

            if (_activeId != 0)
                MirEngine.BrowserAudio.SetSoundVolume(_activeId, Volume);
        }

        public void UpdateFlags()
        {
            // Web Audio 无需重建缓冲
        }
    }
}
