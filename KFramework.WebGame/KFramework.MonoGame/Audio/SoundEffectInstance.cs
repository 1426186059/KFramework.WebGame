using KFramework.JSBind;

namespace KFramework
{

    /// <summary>
    /// 音效的可控播放实例（对齐 MonoGame 的 <c>SoundEffectInstance</c>）。
    /// 由 <see cref="SoundEffect.CreateInstance"/> 创建，可独立控制播放/循环/音量/音高/声相。
    /// </summary>
    public sealed class SoundEffectInstance : IDisposable
    {
        private int _instanceId;
        private bool _disposed;

        internal SoundEffectInstance(int instanceId) => _instanceId = instanceId;

        public bool IsLooped { get; set; }
        public float Volume { get; set; } = 1f;
        public float Pitch { get; set; } = 1f;
        public float Pan { get; set; }
        public SoundState State { get; private set; } = SoundState.Stopped;

        public void Play()
        {
            if (_instanceId == 0) return;   // 缓冲未解码

            JSBind_Audio.SetInstanceLoop(_instanceId, IsLooped);
            JSBind_Audio.SetInstanceVolume(_instanceId, Volume);
            JSBind_Audio.SetInstancePitch(_instanceId, Pitch);
            JSBind_Audio.SetInstancePan(_instanceId, Pan);
            JSBind_Audio.PlayInstance(_instanceId, Volume, Pitch, Pan, IsLooped);
            State = SoundState.Playing;
        }

        public void Stop()
        {
            if (_instanceId != 0) JSBind_Audio.StopInstance(_instanceId);
            State = SoundState.Stopped;
        }

        public void Pause()
        {
            if (_instanceId == 0 || State != SoundState.Playing) return;
            JSBind_Audio.PauseInstance(_instanceId);
            State = SoundState.Paused;
        }

        public void Resume()
        {
            if (_instanceId == 0 || State != SoundState.Paused) return;
            JSBind_Audio.ResumeInstance(_instanceId);
            State = SoundState.Playing;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_instanceId != 0)
            {
                JSBind_Audio.ReleaseInstance(_instanceId);
                _instanceId = 0;
            }
        }
    }
}
