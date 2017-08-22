using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PoGo.DiscordBot.Managers;
using PoGo.DiscordBot.Services;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    public class PoGoBot : IDisposable
    {
        const string Token = "MzQ3ODM2OTIyMDM2NzQ4Mjg5.DHeNAg.X7SXUjVVWteb14T9ewdULDFBB0A";
        public const char Prefix = '!';


        public IServiceProvider ServiceProvider { get; }
        readonly ServiceCollection services;
        readonly LogSeverity LogSeverity;
        readonly DiscordSocketClient client;
        readonly CommandService commands;
        //readonly LogManager logManager;

        public PoGoBot()
        {
#if DEBUG
            LogSeverity = LogSeverity.Debug;
#else
            LogSeverity = LogSeverity.Info;

#endif
            services = new ServiceCollection();
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity,
                MessageCacheSize = 100,
            });
            commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity,
            });

            ServiceProvider = ConfigureServices();

            Init();
        }

        void Init()
        {
            client.Log += Log;
            commands.Log += Log;

            //client.JoinedGuild += JoinedGuild;
            client.GuildAvailable += GuildAvailable;
            client.MessageReceived += HandleCommand;
            client.ReactionAdded += ReactionAdded;
            client.ReactionRemoved += OnReactionRemoved;
        }

        async Task GuildAvailable(SocketGuild guild)
        {
            var raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnNewGuild(guild);
        }

        async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnReactionRemoved(message, channel, reaction);
        }

        async Task ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnReactionAdded(message, channel, reaction);
        }

        async Task JoinedGuild(SocketGuild guild)
        {
            var raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnNewGuild(guild);
        }

        public IServiceProvider ConfigureServices()
        {
            services.AddSingleton(this);
            services.AddSingleton<IDiscordClient>(client);
            services.AddSingleton<RoleService>();
            services.AddSingleton<RaidService>();
            services.AddSingleton<LogManager>();
            services.AddSingleton<StaticRaidChannels>();

            return services.BuildServiceProvider();
        }

        public void Dispose()
        {
            client?.LogoutAsync().Wait();
            client?.Dispose();
        }

        public async Task RunAsync()
        {
            await InitCommands();

            await client.LoginAsync(TokenType.Bot, Token);
            await client.StartAsync();
        }

        async Task InitCommands()
        {
            var modules = await commands.AddModulesAsync(Assembly.GetEntryAssembly());
            await Log("Loading modules");
            foreach (var module in modules)
                await Log($"{module.Name}: {string.Join(", ", module.Commands.Select(t => t.Name))}");
            await Log("Modules loaded");
        }

        async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed succesfully)
            var result = await commands.ExecuteAsync(context, argPos, ServiceProvider);
            if (!result.IsSuccess)
                Console.WriteLine(result.ErrorReason);
            //await context.Channel.SendMessageAsync(result.ErrorReason);
        }

        Task Log(string message)
        {
            return Log(new LogMessage(LogSeverity.Debug, "Code", message));
        }

        Task Log(LogMessage message)
        {
            var cc = Console.ForegroundColor;
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }

            string logMessage = $"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}";
            Console.WriteLine(logMessage);
            //logManager.AddLog(logMessage);
            if (message.Exception != null)
            {
                Console.WriteLine(message.Exception);
                //logManager.AddLog(message.Exception.ToString());
            }
            Console.ForegroundColor = cc;
            return Task.CompletedTask;
        }
    }
}
