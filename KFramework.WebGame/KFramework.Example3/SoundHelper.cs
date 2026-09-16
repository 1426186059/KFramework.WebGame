using KFramework.MonoGame;

namespace KFramework.Example3;

/// <summary>
/// 音频加载适配：源工程用 MonoGame 的 ContentManager.Load&lt;SoundEffect&gt;，
/// Web 端没有该泛型接口，改为从内容包读原始 wav 字节并用 KFramework.MonoGame.SoundEffect 解码。
/// FromBytes 异步解码、Play 在未就绪时直接忽略，不会阻塞 wasm 主线程。
/// </summary>
internal static class SoundHelper
{
    public static SoundEffect LoadSound(this AssetBundle bundle, string name)
    {
        // 资源名统一带原始扩展名（如 .wav），调用处可省略；此处按需补上后缀以匹配包内资源名。
        if (!name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) name += ".wav";
        return SoundEffect.FromBytes(bundle.LoadAsset(name), "audio/wav");
    }
}
