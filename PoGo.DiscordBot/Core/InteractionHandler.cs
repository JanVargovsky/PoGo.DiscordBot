using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Callbacks;
using PoGo.DiscordBot.Configuration.Options;

namespace PoGo.DiscordBot.Core;

public class InteractionHandler : IInteractionCreated, IInitializer, IReady
{
    private readonly ILogger<InteractionHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IOptions<ConfigurationOptions> _configuration;

    public InteractionHandler(
        ILogger<InteractionHandler> logger,
        IServiceProvider serviceProvider,
        DiscordSocketClient client,
        InteractionService interactionService,
        IOptions<ConfigurationOptions> configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _client = client;
        _interactionService = interactionService;
        _configuration = configuration;
    }

    public async ValueTask InitializeAsync()
    {
        var modules = await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
        foreach (var module in modules)
            _logger.LogDebug($"Loaded interactive module {module.Name}: {string.Join(", ", module.SlashCommands.Select(t => t.Name))}");
    }

    public async Task OnReady()
    {
#if DEBUG
        var globalCommands = await _interactionService.RegisterCommandsToGuildAsync(_configuration.Value.Guilds.Single().Id, true);
#else
        var globalCommands = await _interactionService.RegisterCommandsGloballyAsync(true);
#endif
        foreach (var command in globalCommands)
            _logger.LogDebug($"Registered global command {command.Name}");
    }

    public async Task OnInteractionCreated(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
            var context = new SocketInteractionContext(_client, interaction);

            _logger.LogDebug($"Executing interaction command '{ToLog(interaction)}'");

            // Execute the incoming command.
            var result = await _interactionService.ExecuteCommandAsync(context, _serviceProvider);

            if (!result.IsSuccess)
            {
                _logger.LogError($"Interaction failed {result.Error} - {result.ErrorReason}");
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        // implement
                        break;
                    default:
                        break;
                }
            }
        }
        catch
        {
            // If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
            // response, or at least let the user know that something went wrong during the command execution.
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }

    private string ToLog(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand slashCommand => $"{slashCommand.CommandName} {ToLog(slashCommand.Data.Options)}",
        SocketModal socketModal => socketModal.Data.CustomId,
        _ => interaction.GetType().Name,
    };

    private string ToLog(IEnumerable<SocketSlashCommandDataOption> socketSlashCommandDataOptions) => string.Join(' ', socketSlashCommandDataOptions.Select(ToLog));

    private string ToLog(SocketSlashCommandDataOption slashOption) => slashOption.Type switch
    {
        ApplicationCommandOptionType.SubCommand => slashOption.Name,
        ApplicationCommandOptionType.SubCommandGroup => slashOption.Name,
        _ => slashOption.Value?.ToString() ?? "<null>",
    };
}
