namespace KFramework.Example2;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        using var game = new TankGame();
        await game.RunAsync();
    }
}
