using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Callbacks;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Modules.Preconditions;

namespace PoGo.DiscordBot.Core;

public class CommandHandler : IMessageReceived, IInitializer
{
    readonly ILogger<CommandHandler> _logger;
    readonly IServiceProvider _serviceProvider;
    readonly DiscordSocketClient _client;
    readonly CommandService _commands;
    readonly IOptions<ConfigurationOptions> _configuration;

    public CommandHandler(
        ILogger<CommandHandler> logger,
        IServiceProvider serviceProvider,
        DiscordSocketClient client,
        CommandService commands,
        IOptions<ConfigurationOptions> configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _client = client;
        _commands = commands;
        _configuration = configuration;
    }

    public async ValueTask InitializeAsync()
    {
        var modules = await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
        foreach (var module in modules)
            _logger.LogDebug($"Loaded module {module.Name}: {string.Join(", ", module.Commands.Select(t => t.Name))}");
    }

    public async Task OnMessageReceived(SocketMessage socketMessage)
    {
        if (!(socketMessage is SocketUserMessage message))
            return;

        int argPos = 0;
        if (!(message.HasCharPrefix(_configuration.Value.Prefix, ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;

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
            {
                if (result is ParseResult parseResult && parseResult.ErrorParameter != null)
                {
                    reply = string.IsNullOrEmpty(parseResult.ErrorParameter.Summary) ?
                        $"Špatný parametr {parseResult.ErrorParameter.Name}." :
                        $"Špatný parametr {parseResult.ErrorParameter.Name} - {parseResult.ErrorParameter.Summary}";
                }
                else
                    reply = "Špatné parametry.";

            }
            else if (result is TeamPreconditionResult teamResult)
                reply = teamResult.ErrorReason;
            else if (result.Error == CommandError.UnmetPrecondition && result.ErrorReason == "Invalid context for command; accepted contexts: Guild.")
                reply = "Tenhle příkaz tady není dostupný.";

            if (reply != null)
                await context.Channel.SendMessageAsync($"{message.Author.Mention} {reply}");

            _logger.LogDebug($"Command: '{message.Content}', ErrorReason: '{result.ErrorReason}', User: '{message.Author.Username}' ({message.Author.Id}), Reply: '{reply ?? "null"}'");
        }
    }
}
