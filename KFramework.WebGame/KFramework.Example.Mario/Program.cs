namespace KFramework.Example.Mario
{

    internal static class Program
    {
        private static async Task Main(string[] args)
        {

            using var game = new MarioGame();
            await game.RunAsync();

        }
    }

}
