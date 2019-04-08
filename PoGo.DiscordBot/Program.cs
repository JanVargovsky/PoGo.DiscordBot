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
            using (var bot = new PoGoBot())
            {
                await bot.RunAsync();
                await Task.Delay(Timeout.Infinite);
            }
        }
    }
}