using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Services;
using System;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new HostBuilder()
                .ConfigureHostConfiguration(builder =>
                {
                    builder.AddEnvironmentVariables();
                })
                .ConfigureAppConfiguration((hostContext, builder) =>
                {
                    var configuration = hostContext.Configuration;
                    var environment = hostContext.HostingEnvironment.EnvironmentName =
                        configuration.GetValue<string>("PoGoEnvironment") ?? throw new Exception("Unknown environment");
                    builder
                        .SetBasePath(configuration.GetValue("ConfigurationPath", Environment.CurrentDirectory))
                        .AddJsonFile("configuration.json")
                        .AddJsonFile($"configuration.{environment}.json")
                        .AddJsonFile("appsettings.json")
                        .AddJsonFile($"appsettings.{environment}.json");
                })
                .ConfigureLogging((host, builder) =>
                {
                    var configuration = host.Configuration;
                    builder
                        .SetMinimumLevel(configuration.GetValue<LogLevel>("Logging:LogLevel:Default"))
                        .AddConsole()
                        .AddFile(configuration.GetSection("Logging"));
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;
                    services.Configure<ConfigurationOptions>(configuration);

                    var logSeverity = configuration.GetValue<LogSeverity>("Logging:LogLevel:Discord");
                    services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                    {
                        LogLevel = logSeverity,
                        MessageCacheSize = 100,
                    }));
                    services.AddSingleton(new CommandService(new CommandServiceConfig
                    {
                        LogLevel = logSeverity,
                        DefaultRunMode = RunMode.Async,
                    }));
                    services.AddSingleton<InteractiveService>();

                    services.AddSingleton<ConfigurationService>();
                    services.AddSingleton<RaidService>();
                    services.AddSingleton<TeamService>();
                    services.AddSingleton<UserService>();
                    services.AddSingleton<RaidChannelService>();
                    services.AddSingleton<RoleService>();
                    services.AddSingleton<RaidBossInfoService>();
                    services.AddSingleton<GymLocationService>();
                    services.AddSingleton<RaidStorageService>();
                    services.AddSingleton<TimeService>();

                    services.AddSingleton<PoGoBot>();
                    services.AddHostedService<PoGoBotHostedService>();
                })
                .Build()
                .RunAsync();
        }
    }
}
