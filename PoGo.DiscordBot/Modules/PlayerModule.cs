using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Services;
using System;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    public class PlayerModule : ModuleBase<SocketCommandContext>
    {
        private readonly UserService userService;
        private readonly TeamService teamService;

        public PlayerModule(UserService userService, TeamService teamService)
        {
            this.userService = userService;
            this.teamService = teamService;
        }

        [Command("team")]
        public async Task CheckTeam(SocketGuildUser user)
        {
            await userService.CheckTeam(user);
        }

        [Command("team", RunMode = RunMode.Async)]
        public async Task SetTeam(PokemonTeam team)
        {
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
        [TeamPrecondition]
        public async Task SetLevel(int level)
        {
            if (!(Context.User is SocketGuildUser user))
                return;

            if (!(level >= 1 && level <= 40))
            {
                await ReplyAsync("Asi hraješ jinou hru ... povolený level je 1-40");
                return;
            }

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
