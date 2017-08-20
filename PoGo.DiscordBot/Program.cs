using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var bot = new PoGoBot())
            {
                await bot.RunAsync();
                await Task.Delay(-1);
            }
        }

        //static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        //async Task MainAsync()
        //{
        //    var bot = new PoGoBot();
        //    await bot.RunAsync();
        //    await Task.Delay(-1);
        //}
    }
}
