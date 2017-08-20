using Discord.Commands;
using Discord.WebSocket;
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
            availableTeams = new[] { "Mystic", "Instinct", "Valor" };
        }

        [Command("team", RunMode = RunMode.Async)]
        public async Task SetTeam(string teamName)
        {
            var team = availableTeams.FirstOrDefault(t => t.Equals(teamName, StringComparison.InvariantCultureIgnoreCase));
            if (team == null)
                return;

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
    }
}
