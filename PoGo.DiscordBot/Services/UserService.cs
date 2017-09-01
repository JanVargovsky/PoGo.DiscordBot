using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Dto;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class UserService
    {
        private readonly ILogger<UserService> logger;
        private readonly TeamService teamService;

        public UserService(ILogger<UserService> logger, TeamService teamService)
        {
            this.logger = logger;
            this.teamService = teamService;
        }

        public PokemonTeam? GetTeam(SocketGuildUser user)
        {
            var teamRoles = teamService.GuildTeamRoles[user.Guild.Id].RoleTeams;

            foreach (var role in user.Roles)
                if (teamRoles.TryGetValue(role.Id, out var team))
                    return team;

            return null;
        }

        public PlayerDto GetPlayer(SocketGuildUser user) => new PlayerDto
        {
            User = user,
            Team = GetTeam(user),
        };

        public async Task CheckTeam(SocketGuildUser user)
        {
            var team = GetTeam(user);

            if (team == null)
            {
                logger.LogInformation($"Notifying {user.Id} '{user.Nickname ?? user.Username}' about team role");
                await user.SendMessageAsync("Ahoj, nastav si team prosím.");
            }
        }

        public Task OnUserJoined(SocketGuildUser user)
        {
            logger.LogInformation($"User joined {user.Id} '{user.Nickname ?? user.Username}'");
            return CheckTeam(user);
        }
    }
}
