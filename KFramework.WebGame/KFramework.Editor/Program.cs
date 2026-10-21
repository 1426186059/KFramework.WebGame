using System.Threading.Tasks;

namespace KFramework.Editor
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            using var game = new EditorGame();
            await game.RunAsync();
        }
    }
}
