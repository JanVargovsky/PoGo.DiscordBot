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
using PoGo.DiscordBot.Properties;
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

        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly ILogger logger;
        private readonly ConfigurationOptions configuration;
        private readonly Timer updateRaidsTimer;

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

            LogSeverity logSeverity = Enum.Parse<LogSeverity>(Configuration["Logging:LogLevel:Discord"]);
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
                RaidService raidService = (RaidService)state;
                await raidService.UpdateRaidMessages();
            }, ServiceProvider.GetService<RaidService>(), Timeout.Infinite, Timeout.Infinite);

            Init();
        }

        private void Init()
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

        private async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            RaidService raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnMessageDeleted(message, channel);
        }

        private async Task OnUserJoined(SocketGuildUser user)
        {
            UserService userService = ServiceProvider.GetService<UserService>();
            await userService.OnUserJoined(user);
        }

        private async Task GuildAvailable(SocketGuild guild)
        {
            logger.LogInformation($"New guild: '{guild.Name}'");

            TeamService teamService = ServiceProvider.GetService<TeamService>();
            await teamService.OnNewGuild(guild);

            RaidChannelService raidChannelService = ServiceProvider.GetService<RaidChannelService>();
            GuildOptions[] guildOptions = ServiceProvider.GetService<IOptions<ConfigurationOptions>>().Value.Guilds;
            raidChannelService.OnNewGuild(guild, guildOptions);

            RaidService raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnNewGuild(guild);
        }

        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            RaidService raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnReactionRemoved(message, channel, reaction);
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            RaidService raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnReactionAdded(message, channel, reaction);
        }

        private async Task JoinedGuild(SocketGuild guild)
        {
            RaidService raidService = ServiceProvider.GetService<RaidService>();
            await raidService.OnNewGuild(guild);
        }

        private Task LoggedIn()
        {
            logger.LogInformation("Logged in");
            return Task.CompletedTask;
        }

        private Task LoggedOut()
        {
            logger.LogInformation("Logged out");
            return Task.CompletedTask;
        }

        private async Task Connected()
        {
            logger.LogInformation("Connected");
            await client.SetGameAsync(Debugger.IsAttached ? "Debugging" : "Pokémon GO");
            updateRaidsTimer.Change(TimeSpan.FromSeconds(120 - DateTime.Now.Second), TimeSpan.FromMinutes(1));
        }

        private Task Disconnected(Exception exception)
        {
            logger.LogInformation(exception, "Disconnected");
            updateRaidsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public IServiceProvider ConfigureServices()
        {
            ServiceCollection services = new ServiceCollection();

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

        private async Task InitCommands()
        {
            System.Collections.Generic.IEnumerable<ModuleInfo> modules = await commands.AddModulesAsync(Assembly.GetEntryAssembly());
            logger.LogDebug("Loading modules");
            foreach (ModuleInfo module in modules)
                logger.LogDebug($"{module.Name}: {string.Join(", ", module.Commands.Select(t => t.Name))}");
            logger.LogDebug("Modules loaded");
        }

        private async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            if (!(messageParam is SocketUserMessage message))
                return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix(configuration.Prefix, ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            SocketCommandContext context = new SocketCommandContext(client, message);
            // Execute the command. (result does not indicate a return value,
            // rather an object stating if the command executed succesfully)
            IResult result = await commands.ExecuteAsync(context, argPos, ServiceProvider);
            if (!result.IsSuccess)
            {
                if (result.Error == CommandError.BadArgCount)
                {
                    const string TooFewArgs = "The input text has too few parameters.";
                    const string TooManyArgs = "The input text has too many parameters.";
                    if (result.ErrorReason == TooFewArgs)
                        await context.Channel.SendMessageAsync(Resources.TooFewArgs);
                    else if (result.ErrorReason == TooManyArgs)
                        await context.Channel.SendMessageAsync(Resources.TooManyArgs);
                }
                else if (result.Error == CommandError.ParseFailed)
                    await context.Channel.SendMessageAsync(Resources.BadParameters);
                else if (result is TeamPreconditionResult teamResult)
                    await context.Channel.SendMessageAsync(teamResult.ErrorReason);

                logger.LogDebug(result.ErrorReason);
            }
            // await context.Channel.SendMessageAsync(result.ErrorReason);
        }

        private Task Log(LogMessage message)
        {
            LogLevel logLevel = message.Severity.ToLogLevel();
            logger.Log(logLevel, 0, message, null, LogMessageFormatter);

            if (message.Exception != null)
                logger.LogCritical(message.Exception.ToString());

            return Task.CompletedTask;
        }

        private string LogMessageFormatter(LogMessage message, Exception exception)
        {
            return $"{message.Source}: {message.Message}";
        }
    }
}