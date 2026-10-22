using System.Threading.Tasks;

namespace KFramework.Game.Template
{
    /// <summary>
    /// 入口：构造并启动游戏（浏览器里主循环由 requestAnimationFrame 驱动，RunAsync 不会阻塞）。
    /// 这是一个最小可运行的 WASM 游戏模板，依赖 KFramework.MonoGame + KFramework.MonoGameExtend。
    /// </summary>
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            using var game = new GameTemplateGame();
            await game.RunAsync();
        }
    }
}
