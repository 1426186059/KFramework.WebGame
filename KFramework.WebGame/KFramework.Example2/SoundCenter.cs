using System.Diagnostics;
using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>
/// 本游戏的“真实音频”中心：从内容管线加载 wav 资源，封装成 <see cref="SoundEffect"/> 按名播放。
/// 对应 PixiJS 版用真实音效文件播放的模式（不再用合成）。
/// 资源放在 Content/raw/main/MyRes/Audio/ 下，构建时由管线打包为字节资产，
/// 包内资源名为小写 + 带扩展名（如 <c>main/myres/audio/fire.wav</c>）。
/// 逻辑键（shoot/explosion/hit/pickup/powerup/gameover）到真实文件名的映射见 <see cref="AudioPaths"/>。
/// </summary>
internal sealed class SoundCenter
{
    // 逻辑键 → 包内真实资源路径（raw 目录里只有 5 个 wav：Fire/Explosion/Hit/GetBonus/Start）
    private static readonly Dictionary<string, string> AudioPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shoot"]     = "main/myres/audio/fire.wav",
        ["explosion"] = "main/myres/audio/explosion.wav",
        ["hit"]       = "main/myres/audio/hit.wav",
        ["pickup"]    = "main/myres/audio/getbonus.wav",
        ["powerup"]   = "main/myres/audio/start.wav",
        ["gameover"]  = "main/myres/audio/start.wav",
    };

    private readonly Dictionary<string, SoundEffect> _effects = new(StringComparer.OrdinalIgnoreCase);

    public static SoundCenter Load(AssetBundle bundle)
    {
        var center = new SoundCenter();
        foreach (var kv in AudioPaths)
        {
            if (bundle.TryGetAsset(kv.Value, out byte[]? bytes) && bytes is not null)
                center._effects[kv.Key] = SoundEffect.FromBytes(bytes, "audio/wav");
            else
                PrintTool.Log($"[KFramework.MonoGame] 缺少音效资源：{kv.Value}（逻辑键 {kv.Key}）");
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
