using System.Runtime.InteropServices.JavaScript;

namespace KFramework.JSBind;

/// <summary>
/// audio 模块绑定：由 WebAudio 振荡器实时合成音效，不需要任何音频文件。
/// 这里只负责跨语言调用，业务封装见 <c>KFramework.Audio</c>。
/// </summary>
internal static partial class AudioBind
{
    // 注意：函数名就是模块对象上的属性名，不能带 "audio." 前缀
    // —— .NET 会把点号当成嵌套路径去解析，导致 "audio not found"。
    [JSImport("play", "audio")]
    internal static partial void Play(int sfx, float volume, float pitch);

    /// <summary>浏览器要求用户手势后才能启动音频上下文。</summary>
    [JSImport("unlock", "audio")]
    internal static partial void Unlock();

    [JSImport("setMuted", "audio")]
    internal static partial void SetMuted(bool muted);
}
