using KFramework.Test.WebGPU;
using KFramework.MonoGame;

namespace KFramework.Test.WebGPU
{

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // WebGPU 的 requestAdapter / requestDevice 是【异步】的，且 wasm 单线程下不能阻塞等待
            //（JS Promise 要回到事件循环才 resolve），因此必须先异步建好设备再交给 Game。
            // 浏览器不支持 WebGPU 时 CreateAsync 会自动回落 WebGL 2.0（看页面上的后端名即可分辨）。
            GraphicsDevice device = await GraphicsDevice.CreateAsync("#game", antialias: true, preferWebGpu: true);

            var game = new WebGpuTestGame(device);
            await game.RunAsync();
        }
    }

}
