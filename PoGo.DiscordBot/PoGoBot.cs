using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Callbacks;
using PoGo.DiscordBot.Configuration.Options;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PoGo.DiscordBot
{
    public class PoGoBot : IDisposable
    {
        readonly IServiceProvider _serviceProvider;
        readonly DiscordSocketClient _client;
        readonly CommandService _commands;
        readonly ILogger<PoGoBot> _logger;
        readonly IOptions<ConfigurationOptions> _configuration;

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
            _configuration = configuration;

            InitializeCallbacks();
        }

        public async Task RunAsync()
        {
            await _client.LoginAsync(TokenType.Bot, _configuration.Value.Token);
            await _client.StartAsync();
        }

        public async Task StopAsync()
        {
            await _client.StopAsync();
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        void InitializeCallbacks()
        {
            _client.Log += OnLog;
            _commands.Log += OnLog;

            _client.LoggedIn += OnLoggedIn;
            _client.LoggedOut += OnLoggedOut;
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;

            foreach (var service in _serviceProvider.GetServices<IConnected>())
                _client.Connected += service.OnConnected;

            foreach (var service in _serviceProvider.GetServices<IDisconnected>())
                _client.Disconnected += service.OnDisconnected;

            foreach (var service in _serviceProvider.GetServices<IGuildAvailable>())
                _client.GuildAvailable += service.OnGuildAvailable;

            foreach (var service in _serviceProvider.GetServices<IMessageReceived>())
                _client.MessageReceived += service.OnMessageReceived;

            foreach (var service in _serviceProvider.GetServices<IMessageDeleted>())
                _client.MessageDeleted += service.OnMessageDeleted;

            foreach (var service in _serviceProvider.GetServices<IReactionAdded>())
                _client.ReactionAdded += service.OnReactionAdded;

            foreach (var service in _serviceProvider.GetServices<IReactionRemoved>())
                _client.ReactionRemoved += service.OnReactionRemoved;

            foreach (var service in _serviceProvider.GetServices<IUserJoined>())
                _client.UserJoined += service.OnUserJoined;
        }

        Task OnLoggedIn()
        {
            _logger.LogInformation("Logged in");
            return Task.CompletedTask;
        }

        Task OnLoggedOut()
        {
            _logger.LogInformation("Logged out");
            return Task.CompletedTask;
        }

        async Task OnConnected()
        {
            _logger.LogInformation("Connected");
            await _client.SetGameAsync(Debugger.IsAttached ? "Debugging" : "Pokémon GO");
        }

        Task OnDisconnected(Exception exception)
        {
            _logger.LogInformation(exception, "Disconnected");
            return Task.CompletedTask;
        }

        Task OnLog(LogMessage message)
        {
            var logLevel = message.Severity.ToLogLevel();
            _logger.Log(logLevel, message.Exception, message.Message);
            return Task.CompletedTask;
        }
    }
}
