// NAudio 兼容壳（补齐 ThirdParty.cs 未覆盖的部分）：浏览器端无原生音频后端，全部为无操作实现。
// 注意：WaveFormat 主体在 ThirdParty.cs（已改为 partial），此处仅补充工厂方法与 ISampleProvider 体系。
using System;
using System.IO;

namespace NAudio.Wave
{
    public enum PlaybackState { Stopped, Playing, Paused }

    public interface ISampleProvider
    {
        WaveFormat WaveFormat { get; }
        int Read(float[] buffer, int offset, int count);
    }

    public partial class WaveFormat
    {
        public static WaveFormat CreateIeeeFloatWaveFormat(int sampleRate, int channels) =>
            new WaveFormat { SampleRate = sampleRate, Channels = channels, BitsPerSample = 32, Encoding = WaveFormatEncoding.IeeeFloat };
    }

    public class WaveOutEvent : IDisposable
    {
        public float Volume;
        public PlaybackState PlaybackState = PlaybackState.Stopped;
        public event EventHandler PlaybackStopped;
        public void Init(ISampleProvider provider) { }
        public void Play() { PlaybackState = PlaybackState.Playing; }
        public void Stop() { PlaybackState = PlaybackState.Stopped; PlaybackStopped?.Invoke(this, EventArgs.Empty); }
        public void Dispose() { }
    }

    public class MixingSampleProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; set; }
        public bool ReadFully;
        public MixingSampleProvider(WaveFormat format) { WaveFormat = format; }
        public void AddMixerInput(ISampleProvider input) { }
        public int Read(float[] buffer, int offset, int count) => 0;
    }

    public class MonoToStereoSampleProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; set; }
        public MonoToStereoSampleProvider(ISampleProvider input) { WaveFormat = input?.WaveFormat ?? new WaveFormat(); }
        public int Read(float[] buffer, int offset, int count) => 0;
    }

    public class AudioFileReader : ISampleProvider, IDisposable
    {
        public WaveFormat WaveFormat { get; set; } = new WaveFormat { SampleRate = 44100, Channels = 2 };
        public long Length => 0;
        public AudioFileReader() { }
        public AudioFileReader(string fileName) { }
        public int Read(float[] buffer, int offset, int count) => 0;
        public void Seek(int offset, SeekOrigin origin) { }
        public void Dispose() { }
    }
}

namespace NAudio.Wave.SampleProviders
{
}
