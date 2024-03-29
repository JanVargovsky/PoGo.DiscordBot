﻿using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Services;

namespace PoGo.DiscordBot.Modules;

[RequireContext(ContextType.Guild)]
[RequireUserPermission(GuildPermission.Administrator)]
[Group("blame")]
public class BlameModule : ModuleBase<SocketCommandContext>
{
    private readonly UserService userService;
    private readonly ILogger<BlameModule> logger;

    public BlameModule(UserService userService, ILogger<BlameModule> logger)
    {
        this.userService = userService;
        this.logger = logger;
    }

    [Command("level")]
    public async Task ListPlayersWithoutLevel()
    {
        var players = userService.GetPlayers(Context.Guild.Users)
            .Where(t => !t.Level.HasValue);

        string message = string.Join(", ", players);
        await ReplyAsync($"`{message}`");
    }

    [Command("team")]
    public async Task ListPlayersWithoutTeam()
    {
        var players = userService.GetPlayers(Context.Guild.Users)
            .Where(t => !t.Team.HasValue);

        string message = string.Join(", ", players);
        await ReplyAsync($"`{message}`");
    }
}
