using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Dto;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class UserService
    {
        readonly ILogger<UserService> logger;
        readonly TeamService teamService;

        public UserService(ILogger<UserService> logger, TeamService teamService)
        {
            this.logger = logger;
            this.teamService = teamService;
        }

        public int? GetPlayerLevel(SocketGuildUser user)
        {
            var name = user.Nickname ?? user.Username;
            var result = Regex.Match(name, @"\(\d+\)");
            var stringLevel = result.Captures.LastOrDefault()?.Value;
            if (stringLevel != null &&
                int.TryParse(stringLevel.Substring(1, stringLevel.Length - 2), out var level) &&
                level >= 1 && level <= 40)
                return level;
            return null;
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
            Level = GetPlayerLevel(user),
        };

        public async Task CheckTeam(SocketGuildUser user)
        {
            var team = GetTeam(user);

            if (team == null)
            {
                logger.LogInformation($"Notifying {user.Id} '{user.Nickname ?? user.Username}' about team role");
                string userMessage = "Ahoj, nastav si team a level prosím." + Environment.NewLine +
                    "Tyhle příkazy mi nemůžeš psát přímo tady, ale musíš ho psát do některých z kanálů (např. #diskuze)" + Environment.NewLine +
                    "Přípazy piš zvlášť, jeden příkaz = jedna zpráva." + Environment.NewLine +
                    "Team si nastavíš pomocí příkazu team, např. !team mystic" + Environment.NewLine +
                    "Level si nastavíš pomocí příkazu level, např. !level 30" + Environment.NewLine +
                    "Díky!";
                await user.SendMessageAsync(userMessage);
            }
        }

        public Task OnUserJoined(SocketGuildUser user)
        {
            logger.LogInformation($"User joined {user.Id} '{user.Nickname ?? user.Username}'");
            return CheckTeam(user);
        }
    }
}
