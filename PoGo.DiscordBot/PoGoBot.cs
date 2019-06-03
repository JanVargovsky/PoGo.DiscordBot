using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    public class PoGoBot : IDisposable
    {
        public IServiceProvider ServiceProvider { get; }
        public IConfiguration Configuration { get; }

        readonly DiscordSocketClient client;
        readonly CommandService commands;
        readonly ILogger logger;
        readonly ConfigurationOptions configuration;
        readonly Timer updateRaidsTimer;

        public PoGoBot()
        {
            string environment = Environment.GetEnvironmentVariable("PoGoEnvironment");
            if (string.IsNullOrEmpty(environment))
                throw new Exception($"Unknown environment '{environment}'");
            Console.WriteLine($"Environment: {environment}");

            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("configuration.json", false)
                .AddJsonFile($"configuration.{environment}.json", false)
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile($"appsettings.{environment}.json", false)
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
                DefaultRunMode = RunMode.Async,
            });

            ServiceProvider = ConfigureServices();

            logger = ServiceProvider.GetService<ILoggerFactory>()
                .AddConsole(Configuration.GetSection("Logging"))
                .AddDebug()
                .AddFile(Configuration.GetSection("Logging"))
                .CreateLogger<PoGoBot>();

            configuration = ServiceProvider.GetService<IOptions<ConfigurationOptions>>().Value;

            updateRaidsTimer = new Timer(async state =>
            {
                var raidService = (RaidService)state;
                await raidService.UpdateRaidMessages();
            }, ServiceProvider.GetService<RaidService>(), Timeout.Infinite, Timeout.Infinite);

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
            client.MessageDeleted += OnMessageDeleted;
        }

        async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            var raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnMessageDeleted(message, channel);
        }

        async Task OnUserJoined(SocketGuildUser user)
        {
            var userService = ServiceProvider.GetService<UserService>();
            await userService.OnUserJoined(user);
        }

        async Task GuildAvailable(SocketGuild guild)
        {
            logger.LogInformation($"New guild: '{guild.Name}'");

            var teamService = ServiceProvider.GetService<TeamService>();
            await teamService.OnNewGuild(guild);

            var raidChannelService = ServiceProvider.GetService<RaidChannelService>();
            var guildOptions = ServiceProvider.GetService<IOptions<ConfigurationOptions>>().Value.Guilds;
            raidChannelService.OnNewGuild(guild, guildOptions);

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
            await client.SetGameAsync(Debugger.IsAttached ? "Debugging" : "Pokémon GO");
            updateRaidsTimer.Change(TimeSpan.FromSeconds(120 - DateTime.Now.Second), TimeSpan.FromMinutes(1));
        }

        Task Disconnected(Exception exception)
        {
            logger.LogInformation(exception, "Disconnected");
            updateRaidsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddOptions();
            services.Configure<ConfigurationOptions>(Configuration);

            services.AddLogging();
            services.AddSingleton(client);
            services.AddSingleton<ConfigurationService>();
            services.AddSingleton<InteractiveService>();

            services.AddSingleton<RaidService>();
            services.AddSingleton<TeamService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<RaidChannelService>();
            services.AddSingleton<RoleService>();
            services.AddSingleton<RaidBossInfoService>();
            services.AddSingleton<GymLocationService>();
            services.AddSingleton<RaidStorageService>();

            return services.BuildServiceProvider();
        }

        public void Dispose()
        {
            client?.Dispose();
            updateRaidsTimer?.Dispose();
        }

        public async Task RunAsync()
        {
            await InitCommands();

            await client.LoginAsync(TokenType.Bot, configuration.Token);
            logger.LogInformation("START");
            await client.StartAsync();
        }

        async Task InitCommands()
        {
            var modules = await commands.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceProvider);
            logger.LogDebug("Loading modules");
            foreach (var module in modules)
                logger.LogDebug($"{module.Name}: {string.Join(", ", module.Commands.Select(t => t.Name))}");
            logger.LogDebug("Modules loaded");
        }

        async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            if (!(messageParam is SocketUserMessage message))
                return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix(configuration.Prefix, ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new SocketCommandContext(client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed succesfully)
            var result = await commands.ExecuteAsync(context, argPos, ServiceProvider);
            if (!result.IsSuccess)
            {
                if (result.Error == CommandError.BadArgCount)
                {
                    const string TooFewArgs = "The input text has too few parameters.";
                    const string TooManyArgs = "The input text has too many parameters.";
                    if (result.ErrorReason == TooFewArgs)
                        await context.Channel.SendMessageAsync("Chybí některý z parametrů.");
                    else if (result.ErrorReason == TooManyArgs)
                        await context.Channel.SendMessageAsync("Hodně parametrů - nechybí ti tam uvozovky?");
                }
                else if (result.Error == CommandError.ParseFailed)
                    await context.Channel.SendMessageAsync("Špatné parametry.");
                else if (result is TeamPreconditionResult teamResult)
                    await context.Channel.SendMessageAsync(teamResult.ErrorReason);

                logger.LogDebug(result.ErrorReason);
            }
            // await context.Channel.SendMessageAsync(result.ErrorReason);
        }

        Task Log(LogMessage message)
        {
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
