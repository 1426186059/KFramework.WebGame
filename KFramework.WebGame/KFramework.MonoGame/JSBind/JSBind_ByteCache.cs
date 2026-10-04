using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 取 ByteCache 的零拷贝统计文本（TS 侧 custom_data_byte_cache.ts 的 copyFromStats）。
    /// 接入 Game.PrintFrameProfile，与帧时间分解同框打印，确认零拷贝是否真在生效、累计多少次。
    /// </summary>
    internal static partial class JSBind_ByteCache
    {
        [JSImport("copyFromStats", "custom_data_byte_cache")]
        internal static partial string CopyFromStats();
    }
}
