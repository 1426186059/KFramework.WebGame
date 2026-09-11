// 第三方库兼容壳：Sentry / NAudio / SharpDX。
// 浏览器用 WebAudio 与 Canvas 替代原版 DirectSound 音频管线，这里仅提供
// 原版 DXSound / DXSoundManager 引用到的类型与成员，使代码编过。

namespace Sentry
{
    using System;

    public static class SentrySdk
    {
        public static void CaptureException(Exception exception) { }
    }
}

namespace NAudio.Wave
{
    using System;
    using System.IO;

    public enum WaveFormatEncoding
    {
        Unknown = 0,
        Pcm = 1,
        IeeeFloat = 0x3,
        ALaw = 0x6,
        MuLaw = 0x7,
        WaveFormatADpcm = 0x200,
    }

    public class WaveFormat
    {
        public WaveFormatEncoding Encoding { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int AverageBytesPerSecond { get; set; }
        public int BlockAlign { get; set; }
        public int BitsPerSample { get; set; }
    }

    public abstract class WaveStream : Stream
    {
        public abstract WaveFormat WaveFormat { get; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    public class Mp3FileReader : WaveStream
    {
        public Mp3FileReader(string fileName) { }
        public override WaveFormat WaveFormat => null;
    }

    public class WaveFileReader : WaveStream
    {
        public WaveFileReader(string fileName) { }
        public override WaveFormat WaveFormat => null;
    }
}

namespace NAudio.Vorbis
{
    using NAudio.Wave;

    public class VorbisWaveReader : WaveStream
    {
        public VorbisWaveReader(string fileName) { }
        public override WaveFormat WaveFormat => null;
    }
}

namespace SharpDX.Multimedia
{
    public enum WaveFormatEncoding
    {
        Pcm = 0x1,
        IeeeFloat = 0x3,
        WaveFormatADpcm = 0x200,
    }

    public class WaveFormat
    {
        public WaveFormat() { }

        public WaveFormatEncoding Encoding { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int AverageBytesPerSecond { get; set; }
        public int BlockAlign { get; set; }
        public int BitsPerSample { get; set; }

        public static WaveFormat CreateCustomFormat(WaveFormatEncoding encoding, int sampleRate, int channels, int averageBytesPerSecond, int blockAlign, int bitsPerSample)
        {
            return new WaveFormat
            {
                Encoding = encoding,
                SampleRate = sampleRate,
                Channels = channels,
                AverageBytesPerSecond = averageBytesPerSecond,
                BlockAlign = blockAlign,
                BitsPerSample = bitsPerSample,
            };
        }
    }
}

namespace SharpDX.DirectSound
{
    using System;

    [Flags]
    public enum BufferFlags
    {
        ControlVolume = 0x80,
        GlobalFocus = 0x10000,
    }

    public enum PlayFlags
    {
        None = 0,
        Looping = 0x1,
    }

    public enum LockFlags
    {
        EntireBuffer = 0x2,
    }

    [Flags]
    public enum BufferStatus
    {
        Playing = 0x1,
        BufferLost = 0x2,
        Looping = 0x4,
    }

    public enum CooperativeLevel
    {
        Normal = 1,
        Priority = 2,
    }

    public class SoundBufferDescription
    {
        public object Format { get; set; }
        public int BufferBytes { get; set; }
        public BufferFlags Flags { get; set; }
    }

    public class SecondarySoundBuffer
    {
        public SecondarySoundBuffer(object device, SoundBufferDescription description) { }

        public void Play(int reserved, PlayFlags flags) { }
        public bool IsDisposed => false;
        public int CurrentPosition { get; set; }
        public void Stop() { }
        public void Dispose() { }
        public int Volume { get; set; }
        public void Write(byte[] data, int bufferOffset, LockFlags lockFlags) { }
        public void GetCurrentPosition(out int playCursor, out int writeCursor) { playCursor = 0; writeCursor = 0; }
        public BufferStatus Status => BufferStatus.Playing;
    }

    public class DirectSound
    {
        public DirectSound() { }

        public void SetCooperativeLevel(IntPtr windowHandle, CooperativeLevel level) { }
        public bool IsDisposed => false;
        public void Dispose() { }
    }
}
