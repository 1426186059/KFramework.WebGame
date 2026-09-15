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
        => SoundEffect.FromBytes(bundle.LoadAsset(name), "audio/wav");
}
