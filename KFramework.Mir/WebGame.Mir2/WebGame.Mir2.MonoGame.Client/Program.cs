using WebGame.Mir2.MonoGame.Client;

// 浏览器端入口：WASM 启动后由运行时调用 Main。
// 真正的每帧循环由 KFramework.MonoGame 的 GameHost（KFramework.TSEngine 的 jsengine/main.js 经
// requestAnimationFrame 驱动）接管：它在启动时把 JSBind_GameHost.Current 指向 MirGame，
// 之后每帧回调 JSBind_GameHost.Frame → MirGame.TickFrame。这里仅负责创建并启动游戏。
class Program
{
    static void Main()
    {
        _ = new MirGame().RunAsync();
    }
}
