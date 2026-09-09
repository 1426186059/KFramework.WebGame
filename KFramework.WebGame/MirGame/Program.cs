using KFramework;

namespace MirGame;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        using var game = new StarDefenderGame();
        await game.RunAsync();
    }
}
