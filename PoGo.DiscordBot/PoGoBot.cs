using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    public class PoGoBot : IAsyncDisposable
    {
        readonly IServiceProvider _serviceProvider;
        readonly DiscordSocketClient _client;
        readonly CommandService _commands;
        readonly ILogger<PoGoBot> _logger;
        readonly ConfigurationOptions _configuration;
        readonly Timer _updateRaidsTimer;

        public PoGoBot(
            IServiceProvider serviceProvider,
            DiscordSocketClient client,
            CommandService commands,
            ILogger<PoGoBot> logger,
            IOptions<ConfigurationOptions> configuration)
        {
            _serviceProvider = serviceProvider;
            _client = client;
            _commands = commands;
            _logger = logger;
            _configuration = configuration.Value;

            _updateRaidsTimer = new Timer(async state =>
            {
                var raidService = (RaidService)state;
                await raidService.UpdateRaidMessages();
            }, _serviceProvider.GetService<RaidService>(), Timeout.Infinite, Timeout.Infinite);

            Initialize();
        }

        void Initialize()
        {
            _client.Log += Log;
            _commands.Log += Log;

            _client.LoggedIn += LoggedIn;
            _client.LoggedOut += LoggedOut;

            _client.JoinedGuild += JoinedGuild;
            _client.Connected += Connected;
            _client.Disconnected += Disconnected;
            _client.GuildAvailable += GuildAvailable;
            _client.MessageReceived += HandleCommand;
            _client.ReactionAdded += ReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
            _client.UserJoined += OnUserJoined;
            _client.MessageDeleted += OnMessageDeleted;
        }

        async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            var raidService = _serviceProvider.GetService<RaidService>();
            await raidService.OnMessageDeleted(message, channel);
        }

        async Task OnUserJoined(SocketGuildUser user)
        {
            var userService = _serviceProvider.GetService<UserService>();
            await userService.OnUserJoined(user);
        }

        async Task GuildAvailable(SocketGuild guild)
        {
            _logger.LogInformation($"New guild: '{guild.Name}'");

            var teamService = _serviceProvider.GetService<TeamService>();
            await teamService.OnNewGuild(guild);

            var raidChannelService = _serviceProvider.GetService<RaidChannelService>();
            var guildOptions = _serviceProvider.GetService<IOptions<ConfigurationOptions>>().Value.Guilds;
            raidChannelService.OnNewGuild(guild, guildOptions);

            var raidService = _serviceProvider.GetService<RaidService>();
            await raidService.OnNewGuild(guild);
        }

        async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var raidService = _serviceProvider.GetService<RaidService>();
            await raidService.OnReactionRemoved(message, channel, reaction);
        }

        async Task ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var raidService = _serviceProvider.GetService<RaidService>();
            await raidService.OnReactionAdded(message, channel, reaction);
        }

        async Task JoinedGuild(SocketGuild guild)
        {
            var raidService = _serviceProvider.GetService<RaidService>();
            await raidService.OnNewGuild(guild);
        }

        Task LoggedIn()
        {
            _logger.LogInformation("Logged in");
            return Task.CompletedTask;
        }

        Task LoggedOut()
        {
            _logger.LogInformation("Logged out");
            return Task.CompletedTask;
        }

        async Task Connected()
        {
            _logger.LogInformation("Connected");
            await _client.SetGameAsync(Debugger.IsAttached ? "Debugging" : "Pokémon GO");
            _updateRaidsTimer.Change(TimeSpan.FromSeconds(120 - DateTime.UtcNow.Second), TimeSpan.FromMinutes(1));
        }

        Task Disconnected(Exception exception)
        {
            _logger.LogInformation(exception, "Disconnected");
            _updateRaidsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();
            if (_updateRaidsTimer != null)
                await _updateRaidsTimer.DisposeAsync();
        }

        public async Task RunAsync()
        {
            await InitCommands();
            await _client.LoginAsync(TokenType.Bot, _configuration.Token);
            await _client.StartAsync();
        }

        public async Task StopAsync()
        {
            await _client.StopAsync();
        }

        async Task InitCommands()
        {
            var modules = await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
            foreach (var module in modules)
                _logger.LogDebug($"Loaded module {module.Name}: {string.Join(", ", module.Commands.Select(t => t.Name))}");
        }

        async Task HandleCommand(SocketMessage messageParam)
        {
            if (!(messageParam is SocketUserMessage message))
                return;
            int argPos = 0;
            if (!(message.HasCharPrefix(_configuration.Prefix, ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;

            var context = new SocketCommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, argPos, _serviceProvider);
            if (!result.IsSuccess)
            {
                string reply = null;
                if (result.Error == CommandError.BadArgCount)
                {
                    const string TooFewArgs = "The input text has too few parameters.";
                    const string TooManyArgs = "The input text has too many parameters.";
                    if (result.ErrorReason == TooFewArgs)
                        reply = "Chybí některý z parametrů.";
                    else if (result.ErrorReason == TooManyArgs)
                        reply = "Hodně parametrů - nechybí ti tam uvozovky?";
                }
                else if (result.Error == CommandError.ParseFailed)
                    reply = "Špatné parametry.";
                else if (result is TeamPreconditionResult teamResult)
                    reply = teamResult.ErrorReason;
                else if (result.Error == CommandError.UnmetPrecondition && result.ErrorReason == "Invalid context for command; accepted contexts: Guild.")
                    reply = "Tenhle příkaz tady není dostupný.";

                if (reply != null)
                    await context.Channel.SendMessageAsync($"{message.Author.Mention} {reply}");

                _logger.LogDebug(result.ErrorReason);
            }
        }

        Task Log(LogMessage message)
        {
            var logLevel = message.Severity.ToLogLevel();
            _logger.Log(logLevel, message.Exception, message.Message);
            return Task.CompletedTask;
        }
    }
}
