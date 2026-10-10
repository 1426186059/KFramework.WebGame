using KFramework.Test.WebGPU;
using KFramework.MonoGame;

namespace KFramework.Test.WebGPU
{

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // 设备由 Game 在 Run 进入 Initialize 之前异步创建（照 MonoGame 的 DoInitialize）：
            // WebGpuTestGame 构造时指定 GraphicsBackendKind.WebGPU，Game 内部会 await GraphicsDevice.CreateAsync
            // （WebGPU 优先，不支持回落 WebGL 2.0）。这里不再手动建设备、也不注入 device。
            var game = new WebGpuTestGame();
            await game.RunAsync();
        }
    }

}
