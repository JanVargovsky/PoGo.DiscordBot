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
        public async Task SetLevel(int level)
        {
            if (!(Context.User is SocketGuildUser user))
                return;

            await user.ModifyAsync(t =>
            {
                string name = user.Nickname ?? user.Username;

                // remove previous level
                if (name.EndsWith(')'))
                {
                    int index = name.IndexOf('(');
                    if (index != -1)
                        name = name.Substring(0, index);
                    name = name.TrimEnd();
                }

                t.Nickname = $"{name} ({level})";
            });
        }
    }
}
