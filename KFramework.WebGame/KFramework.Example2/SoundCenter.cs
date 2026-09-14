using System.Diagnostics;
using KFramework.Content;

namespace KFramework.Example2;

/// <summary>
/// 本游戏的“真实音频”中心：从内容管线加载 wav 资源，封装成 <see cref="SoundEffect"/> 按名播放。
/// 对应 PixiJS 版用真实音效文件播放的模式（不再用合成）。
/// 资源放在 Content/raw/audio/ 下，构建时由管线打包为字节资产（audio/shoot、audio/explosion …）。
/// </summary>
internal sealed class SoundCenter
{
    private readonly Dictionary<string, SoundEffect> _effects = new(StringComparer.OrdinalIgnoreCase);

    public static SoundCenter Load(ContentManager content)
    {
        var center = new SoundCenter();
        foreach (string key in new[] { "shoot", "explosion", "hit", "pickup", "powerup", "gameover" })
        {
            if (content.TryLoadBytes("audio/" + key, out byte[]? bytes) && bytes is not null)
                center._effects[key] = SoundEffect.FromBytes(bytes, "audio/wav");
            else
                Console.WriteLine($"[KFramework] 缺少音效资源：audio/{key}");
        }
        return center;
    }

    /// <summary>播放一个音效；资源缺失时静默忽略。</summary>
    public void Play(string key)
    {
        if (_effects.TryGetValue(key, out SoundEffect? effect))
            effect.Play();
    }
}
