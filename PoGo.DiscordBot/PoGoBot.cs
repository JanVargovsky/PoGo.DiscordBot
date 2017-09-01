using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    public class PoGoBot : IDisposable
    {
        const string Token = "MzQ3ODM2OTIyMDM2NzQ4Mjg5.DHeNAg.X7SXUjVVWteb14T9ewdULDFBB0A";
        const string Environment =
#if DEBUG
            "Development";
#else
            "Production";
#endif
        public const char Prefix = '!';

        public IServiceProvider ServiceProvider { get; }
        public IConfiguration Configuration { get; }

        readonly DiscordSocketClient client;
        readonly CommandService commands;
        ILogger logger;

        public PoGoBot()
        {
            Console.WriteLine($"Environment: {Environment}");
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile($"appsettings.{Environment}.json", false)
                .Build();


            var logSeverity = Enum.Parse<LogSeverity>(Configuration["Logging:LogLevel:Discord"]);
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = logSeverity,
                MessageCacheSize = 100,
            });
            commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = logSeverity,
            });

            ServiceProvider = ConfigureServices();

            logger = ServiceProvider.GetService<ILoggerFactory>()
                .AddConsole(Configuration.GetSection("Logging"))
                .AddDebug()
                .AddFile(Configuration.GetSection("Logging"))
                .CreateLogger<PoGoBot>();

            logger.LogTrace("START TRACE");
            logger.LogDebug("START DEBUG");
            logger.LogInformation("START INFO");
            logger.LogWarning("START WARNING");
            logger.LogError("START ERROR");
            logger.LogCritical("START CRITICAL");

            Init();
        }

        void Init()
        {
            client.Log += Log;
            commands.Log += Log;

            client.LoggedIn += LoggedIn;
            client.LoggedOut += LoggedOut;

            client.Connected += Connected;
            client.Disconnected += Disconnected;
            client.GuildAvailable += GuildAvailable;
            client.MessageReceived += HandleCommand;
            client.ReactionAdded += ReactionAdded;
            client.ReactionRemoved += OnReactionRemoved;
            client.UserJoined += OnUserJoined;
        }

        async Task OnUserJoined(SocketGuildUser user)
        {
            var userService = ServiceProvider.GetService<UserService>();
            await userService.OnUserJoined(user);
        }

        async Task GuildAvailable(SocketGuild guild)
        {
            var teamService = ServiceProvider.GetService<TeamService>();
            await teamService.OnNewGuild(guild);

            var raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnNewGuild(guild);

            logger.LogInformation($"New guild: '{guild.Name}'");
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

        Task LoggedIn()
        {
            logger.LogInformation("Logged in");
            return Task.CompletedTask;
        }

        Task LoggedOut()
        {
            logger.LogInformation("Logged out");
            return Task.CompletedTask;
        }

        async Task Connected()
        {
            logger.LogInformation("Connected");
            if (Debugger.IsAttached)
                await client.SetGameAsync("Debugging");
        }

        Task Disconnected(Exception exception)
        {
            logger.LogInformation(exception, "Disconnected");
            return Task.CompletedTask;
        }

        public IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddLogging();
            services.AddSingleton<IDiscordClient>(client);
            services.AddSingleton<StaticRaidChannels>();

            services.AddSingleton<RaidService>();
            services.AddSingleton<TeamService>();
            services.AddSingleton<UserService>();

            return services.BuildServiceProvider();
        }

        public void Dispose()
        {
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
            logger.LogDebug("Loading modules");
            foreach (var module in modules)
                logger.LogDebug($"{module.Name}: {string.Join(", ", module.Commands.Select(t => t.Name))}");
            logger.LogDebug("Modules loaded");
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
            {
                if (result.Error.Value == CommandError.BadArgCount)
                    await context.Channel.SendMessageAsync("Nesedí počet parametrů - nechybí ti tam uvozovky?");
                Console.WriteLine(result.ErrorReason);
            }
            // await context.Channel.SendMessageAsync(result.ErrorReason);
        }

        Task Log(LogMessage message)
        {
            //var cc = Console.ForegroundColor;
            //switch (message.Severity)
            //{
            //    case LogSeverity.Critical:
            //    case LogSeverity.Error:
            //        Console.ForegroundColor = ConsoleColor.Red;
            //        break;
            //    case LogSeverity.Warning:
            //        Console.ForegroundColor = ConsoleColor.Yellow;
            //        break;
            //    case LogSeverity.Info:
            //        Console.ForegroundColor = ConsoleColor.White;
            //        break;
            //    case LogSeverity.Verbose:
            //    case LogSeverity.Debug:
            //        Console.ForegroundColor = ConsoleColor.DarkGray;
            //        break;
            //}

            //string logMessage = $"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message}";
            //Console.WriteLine(logMessage);
            //Console.ForegroundColor = cc;

            LogLevel logLevel = message.Severity.ToLogLevel();
            logger.Log(logLevel, 0, message, null, LogMessageFormatter);

            if (message.Exception != null)
                logger.LogCritical(message.Exception.ToString());

            return Task.CompletedTask;
        }

        string LogMessageFormatter(LogMessage message, Exception exception)
        {
            return $"{message.Source}: {message.Message}";
        }
    }
}
