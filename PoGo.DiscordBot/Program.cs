using PoGo.DiscordBot.Properties;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            using (PoGoBot bot = new PoGoBot())
            {
                Console.WriteLine(Resources.FullUse);
                await bot.RunAsync();
                await Task.Delay(Timeout.Infinite);
            }
        }
    }
}