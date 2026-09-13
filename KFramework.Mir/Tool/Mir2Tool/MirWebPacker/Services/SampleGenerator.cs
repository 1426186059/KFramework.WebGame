using System.Text;
using Mir.Lib;
using SkiaSharp;

namespace MirWebPacker.Services;

/// <summary>
/// 当没有真实地图资源时，生成一个最小可打包的示例目录：
/// data/（txt+json）、textures/（一张 PNG，打包时转 webp）、audio/（一段 wav）。
/// </summary>
public static class SampleGenerator
{
    public static void Generate(string inputDir)
    {
        // 扁平结构：打包器会按扩展名自动归类到 data/ textures/ audio/
        Directory.CreateDirectory(inputDir);

        File.WriteAllText(Path.Combine(inputDir, "map.json"),
            "{\n  \"width\": 100,\n  \"height\": 100,\n  \"spawns\": [ { \"x\": 50, \"y\": 50, \"name\": \"出生点\" } ]\n}");
        File.WriteAllText(Path.Combine(inputDir, "info.txt"),
            "3-1 比奇城外\n示例地图，用于验证 .web.lib 打包流程\n");

        // 生成一张 PNG（打包时演示 png -> 无损 webp 转码）
        using var bmp = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.SkyBlue);
            using var paint = new SKPaint { Color = SKColors.Orange, IsAntialias = true };
            canvas.DrawCircle(32, 32, 20, paint);
        }
        SkiaBitmaps.SavePng(bmp, Path.Combine(inputDir, "t1.png"));

        // 生成一段最小有效 WAV（440Hz 正弦蜂鸣 0.2s，PCM16 单声道）
        WriteBeepWav(Path.Combine(inputDir, "hit01.wav"), 440, 0.2);
    }

    private static void WriteBeepWav(string path, double freq, double seconds)
    {
        const int sampleRate = 8000;
        var samples = (int)(sampleRate * seconds);
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        var ascii = Encoding.ASCII;

        bw.Write(ascii.GetBytes("RIFF"));
        bw.Write(36 + samples * 2);
        bw.Write(ascii.GetBytes("WAVE"));
        bw.Write(ascii.GetBytes("fmt "));
        bw.Write(16);                       // PCM 子块大小
        bw.Write((short)1);                 // 音频格式 = PCM
        bw.Write((short)1);                 // 声道数 = 单声道
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);           // 字节率
        bw.Write((short)2);                 // 块对齐
        bw.Write((short)16);                // 位深
        bw.Write(ascii.GetBytes("data"));
        bw.Write(samples * 2);

        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            short s = (short)(Math.Sin(2 * Math.PI * freq * t) * 3000);
            bw.Write(s);
        }
    }
}
