using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    public class PoGoBotHostedService : IHostedService, IAsyncDisposable
    {
        readonly PoGoBot _bot;
        readonly IServiceProvider _services;

        public PoGoBotHostedService(PoGoBot bot, IServiceProvider services)
        {
            _bot = bot;
            _services = services;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _bot.RunAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _bot.StopAsync();

            foreach (var disposable in _services.GetServices<IAsyncDisposable>())
                await disposable.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _bot.DisposeAsync();
        }
    }
}
