using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Callbacks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class TeamService : IGuildAvailable
    {
        readonly ILogger<TeamService> logger;

        public ConcurrentDictionary<ulong, TeamRolesDto> GuildTeamRoles { get; } // <guildId, roles>

        public TeamService(ILogger<TeamService> logger)
        {
            GuildTeamRoles = new ConcurrentDictionary<ulong, TeamRolesDto>();
            this.logger = logger;
        }

        public async Task OnGuildAvailable(SocketGuild socketGuild)
        {
            GuildTeamRoles[socketGuild.Id] = await GetTeamRoles(socketGuild);
        }

        async Task<IRole> GetOrCreateRole(IGuild guild, PokemonTeam pokemonTeam)
        {
            var role = guild.Roles.FirstOrDefault(t => Enum.TryParse<PokemonTeam>(t.Name, out var team) && pokemonTeam == team);
            if (role == null)
            {
                logger.LogInformation($"Creating new role for team {pokemonTeam}");
                role = await guild.CreateRoleAsync(pokemonTeam.ToString(), color: TeamRoleColors.GetColor(pokemonTeam), isHoisted: true);
                await role.ModifyAsync(t =>
                {
                    t.Mentionable = true;
                });
            }

            return role;
        }

        async Task<TeamRolesDto> GetTeamRoles(IGuild guild)
        {
            var roleIdtoTeam = new Dictionary<ulong, PokemonTeam>();
            var teamToRole = new Dictionary<PokemonTeam, IRole>();

            foreach (PokemonTeam team in Enum.GetValues(typeof(PokemonTeam)))
            {
                var role = await GetOrCreateRole(guild, team);

                roleIdtoTeam[role.Id] = team;
                teamToRole[team] = role;
            }

            return new TeamRolesDto
            {
                RoleTeams = roleIdtoTeam,
                TeamRoles = teamToRole,
            };
        }
    }
}
