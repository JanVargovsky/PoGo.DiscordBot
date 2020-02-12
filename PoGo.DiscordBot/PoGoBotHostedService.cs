using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    public class PoGoBotHostedService : IHostedService, IAsyncDisposable
    {
        readonly PoGoBot _bot;

        public PoGoBotHostedService(PoGoBot bot)
        {
            _bot = bot;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _bot.RunAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _bot.StopAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _bot.DisposeAsync();
        }
    }
}
