﻿using System;
using System.Threading.Tasks;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Core;
using PoGo.DiscordBot.Services;

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
                        .AddFilter("Discord", configuration.GetValue<LogLevel>("Logging:LogLevel:Discord"))
                        .AddConsole()
                        .AddFile(configuration.GetSection("Logging"));
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<ConfigurationOptions>(hostContext.Configuration);
                })
                .UseServiceProviderFactory(new DryIocServiceProviderFactory(new Container(rules => rules.WithDefaultReuse(Reuse.Singleton))))
                .ConfigureContainer<IContainer>((hostContext, container) =>
                {
                    var logSeverity = hostContext.Configuration.GetValue<LogLevel>("Logging:LogLevel:Discord").ToLogLevel();
                    container.RegisterInstance(new DiscordSocketClient(new DiscordSocketConfig
                    {
                        LogLevel = logSeverity,
                        MessageCacheSize = 100,
                    }));
                    container.RegisterInstance(new CommandService(new CommandServiceConfig
                    {
                        LogLevel = logSeverity,
                        DefaultRunMode = RunMode.Async,
                    }));
                    container.Register<InteractiveService>();

                    container.Register<ConfigurationService>();
                    container.RegisterMany<TeamService>();
                    container.RegisterMany<RaidService>();
                    container.RegisterMany<UserService>();
                    container.RegisterMany<RaidChannelService>();
                    container.Register<RoleService>();
                    container.Register<RaidBossInfoService>();
                    container.Register<GymLocationService>();
                    container.Register<RaidStorageService>();
                    container.Register<TimeService>();
                    container.RegisterMany<CommandHandler>();

                    container.RegisterMany<PoGoBot>();
                    container.RegisterMany<PoGoBotHostedService>();
                })
                .Build()
                .RunAsync();
        }
    }
}
