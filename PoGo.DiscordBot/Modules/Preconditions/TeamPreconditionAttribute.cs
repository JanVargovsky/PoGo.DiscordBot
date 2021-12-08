using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PoGo.DiscordBot.Services;

namespace PoGo.DiscordBot.Modules.Preconditions;

public class TeamPreconditionAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        if (!(context.User is SocketGuildUser guildUser))
            return Task.FromResult<PreconditionResult>(TeamPreconditionResult.Fail);

        var userService = services.GetService<UserService>();
        var team = userService.GetTeam(guildUser);

        if (team == null)
            return Task.FromResult<PreconditionResult>(TeamPreconditionResult.Fail);

        return Task.FromResult<PreconditionResult>(TeamPreconditionResult.Success);
    }
}

public class TeamPreconditionResult : PreconditionResult
{
    protected TeamPreconditionResult(CommandError? error, string errorReason)
        : base(error, errorReason)
    {
    }

    public static TeamPreconditionResult Success => new TeamPreconditionResult(null, null);
    public static TeamPreconditionResult Fail => new TeamPreconditionResult(CommandError.UnmetPrecondition, "Je nutné si zvolit team.");
}
