using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PoGo.DiscordBot.Core;

namespace PoGo.DiscordBot;

public class PoGoBotHostedService : IHostedService
{
    private readonly PoGoBot _bot;
    private readonly IServiceProvider _services;

    public PoGoBotHostedService(PoGoBot bot, IServiceProvider services)
    {
        _bot = bot;
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var item in _services.GetServices<IInitializer>())
            await item.InitializeAsync();

        await _bot.RunAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _bot.StopAsync();

        foreach (var disposable in _services.GetServices<IAsyncDisposable>())
            await disposable.DisposeAsync();
    }
}
