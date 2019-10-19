using System.Threading;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await using var bot = new PoGoBot();
            await bot.RunAsync();
            await Task.Delay(Timeout.Infinite);
        }
    }
}
