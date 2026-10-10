using KFramework.Test.WebGL20;

namespace KFramework.Test.WebGL20
{

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // 例子2 固定走 WebGL 2.0：设备可在构造函数里同步创建。
            // （Canvas2D 后端有独立工程：KFramework.Test.Canvas2D）
            var game = new WebGl20TestGame();
            await game.RunAsync();
        }
    }

}
