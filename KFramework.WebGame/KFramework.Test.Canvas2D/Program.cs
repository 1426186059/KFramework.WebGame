using KFramework.Test.Canvas2D;

namespace KFramework.Test.Canvas2D
{

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // 本工程固定使用 Canvas2D 后端（GraphicsBackendKind.Canvas2D，初始化同步完成）。
            var game = new Canvas2DTestGame();
            await game.RunAsync();
        }
    }

}
