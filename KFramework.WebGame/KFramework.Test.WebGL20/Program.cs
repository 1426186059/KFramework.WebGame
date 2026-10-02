using KFramework.Test.WebGL20;

namespace KFramework.Test.WebGL20
{

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // 例子2 固定走 WebGL 2.0：设备可在构造函数里同步创建。
            var game = new WebGl20TestGame();
            await game.RunAsync();
        }
    }

}
