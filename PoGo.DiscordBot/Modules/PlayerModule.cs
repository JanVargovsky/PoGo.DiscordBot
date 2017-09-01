using Discord.Commands;
using Discord.WebSocket;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Services;
using System;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class PlayerModule : ModuleBase
    {
        private readonly UserService userService;
        private readonly TeamService teamService;

        public PlayerModule(UserService userService, TeamService teamService)
        {
            this.userService = userService;
            this.teamService = teamService;
        }

        [Command("team", RunMode = RunMode.Async)]
        public async Task SetTeam(string teamName)
        {
            if (!Enum.TryParse<PokemonTeam>(teamName, true, out var team))
            {
                await ReplyAsync("Takový team neexistuje.");
                return;
            }

            var contextUser = Context.User;
            var user = contextUser as SocketGuildUser;
            if (user == null)
                return;

            var userTeam = userService.GetTeam(user);
            if (userTeam != null)
            {
                await ReplyAsync("Už jsi v teamu.");
                return;
            }

            var role = teamService.GuildTeamRoles[Context.Guild.Id].TeamRoles[team];
            await user.AddRoleAsync(role);
        }

        [Command("level", RunMode = RunMode.Async)]
        public Task SetLevel(int level)
        {
            //var guildUser = await Context.Guild.GetCurrentUserAsync();
            return Task.CompletedTask;
        }
    }
}
