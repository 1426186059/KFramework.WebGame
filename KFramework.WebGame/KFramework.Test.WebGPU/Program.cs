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
            // antialias 管的是【画布】的采样数（离屏 RT 的 MSAA 由各自 MultiSampleCount 决定，与此无关）。
            // 当前用 false：WebGPU 下画布一旦带多重采样+解析目标，切回画布时用 load 续画就保不住内容
            // （"测试来回切"会丢掉切换前画的东西）。这是验证该问题的临时设置。
            GraphicsDevice device = await GraphicsDevice.CreateAsync("#game", antialias: false, preferWebGpu: true);

            var game = new WebGpuTestGame(device);
            await game.RunAsync();
        }
    }

}
