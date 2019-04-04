using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class TeamService
    {
        private readonly ILogger<TeamService> logger;

        public ConcurrentDictionary<ulong, TeamRolesDto> GuildTeamRoles { get; } // <guildId, roles>

        public TeamService(ILogger<TeamService> logger)
        {
            GuildTeamRoles = new ConcurrentDictionary<ulong, TeamRolesDto>();
            this.logger = logger;
        }

        public async Task OnNewGuild(SocketGuild socketGuild)
        {
            GuildTeamRoles[socketGuild.Id] = await GetTeamRoles(socketGuild);
        }

        private async Task<IRole> GetOrCreateRole(IGuild guild, PokemonTeam pokemonTeam)
        {
            IRole role = guild.Roles.FirstOrDefault(t => Enum.TryParse<PokemonTeam>(t.Name, out PokemonTeam team) && pokemonTeam == team);
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

        private async Task<TeamRolesDto> GetTeamRoles(IGuild guild)
        {
            Dictionary<ulong, PokemonTeam> roleIdtoTeam = new Dictionary<ulong, PokemonTeam>();
            Dictionary<PokemonTeam, IRole> teamToRole = new Dictionary<PokemonTeam, IRole>();

            foreach (PokemonTeam team in Enum.GetValues(typeof(PokemonTeam)))
            {
                IRole role = await GetOrCreateRole(guild, team);

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