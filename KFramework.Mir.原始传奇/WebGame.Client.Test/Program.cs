using System.Threading.Tasks;

namespace WebGame.Client.Test
{
    /// <summary>
    /// 入口：构造并启动游戏（WASM 主循环由 requestAnimationFrame 驱动，RunAsync 不会阻塞）。
    /// 本 Test 工程用于对比图集贴图在引擎里的真实颜色（黑/白），定位“聊天背景发白”问题。
    /// </summary>
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            using var game = new AtlasTestGame();
            await game.RunAsync();
        }
    }
}
