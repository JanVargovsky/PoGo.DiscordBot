using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PoGo.DiscordBot.Dto;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class UserModule : ModuleBase
    {
        static readonly string[] availableTeams;

        static UserModule()
        {
            availableTeams = Enum.GetNames(typeof(PokemonTeam));
        }

        [Command("team", RunMode = RunMode.Async)]
        public async Task SetTeam(string teamName)
        {
            if (!Enum.TryParse(typeof(PokemonTeam), teamName, true, out var teamObj))
                return;

            var team = (PokemonTeam)teamObj;

            var contextUser = Context.User;
            var user = contextUser as SocketGuildUser;
            if (user == null)
                return;

            if (user.Roles.Any(t => availableTeams.Any(tt => tt.Equals(t.Name, StringComparison.InvariantCultureIgnoreCase))))
                return;

            var role = Context.Guild.Roles.First(t => t.Name.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));
            await user.AddRoleAsync(role);
        }

        [Command("level", RunMode = RunMode.Async)]
        public Task SetLevel(int level)
        {
            return Task.CompletedTask;
        }

        public static PokemonTeam? GetTeam(SocketGuildUser user)
        {
            var role = user.Roles.FirstOrDefault(t => availableTeams.Any(tt => tt.Equals(t.Name, StringComparison.InvariantCultureIgnoreCase)));

            if (role == null || !Enum.TryParse(typeof(PokemonTeam), role.Name, true, out var teamObj) || !(teamObj is PokemonTeam team))
                return null;

            return team;
        }
    }
}
