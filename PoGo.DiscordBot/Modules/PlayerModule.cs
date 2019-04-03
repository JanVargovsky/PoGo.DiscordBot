using Discord.Commands;
using Discord.WebSocket;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Services;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    public class PlayerModule : ModuleBase<SocketCommandContext>
    {
        readonly UserService userService;
        readonly TeamService teamService;

        public PlayerModule(UserService userService, TeamService teamService)
        {
            this.userService = userService;
            this.teamService = teamService;
        }

        [Command("team")]
        [Summary("CheckTeamSummary")]
        public async Task CheckTeam(
            [Summary("ControlledUser")]SocketGuildUser user)
        {
            await userService.CheckTeam(user);
        }

        [Command("team", RunMode = RunMode.Async)]
        [Summary("SetTeamSummary")]
        public async Task SetTeam(
            [Summary("SelectedTeam")]PokemonTeam team)
        {
            var contextUser = Context.User;
            if (!(contextUser is SocketGuildUser user))
                return;

            var userTeam = userService.GetTeam(user);
            if (userTeam != null)
            {
                await ReplyAsync(LocalizationService.Instance.GetStringFromResources("InTeam"));
                return;
            }

            var role = teamService.GuildTeamRoles[Context.Guild.Id].TeamRoles[team];
            await user.AddRoleAsync(role);
        }

        [Command("level", RunMode = RunMode.Async)]
        [Alias("lvl")]
        [TeamPrecondition]
        [Summary("SetLevelSummary")]
        public async Task SetLevel(
            [Summary("CurrentLevel")]int level)
        {
            if (!(Context.User is SocketGuildUser user))
                return;

            if (!(level >= 1 && level <= 40))
            {
                await ReplyAsync(LocalizationService.Instance.GetStringFromResources("PlayAnotherGame"));
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

        [Command("set")]
        [Summary("SetBasicInfoSummary")]
        public async Task SetBasicInfo(
            [Summary("SelectedTeam")]PokemonTeam team,
            [Summary("CurrentLevel")]int level)
        {
            await SetTeam(team);
            await SetLevel(level);
        }
    }
}
