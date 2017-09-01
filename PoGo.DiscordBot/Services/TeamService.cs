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

        async Task<IRole> GetOrCreateRole(IGuild guild, PokemonTeam pokemonTeam)
        {
            var role = guild.Roles.FirstOrDefault(t => Enum.TryParse<PokemonTeam>(t.Name, out var team) && pokemonTeam == team);
            if (role == null)
            {
                logger.LogInformation($"Creating new role for team {pokemonTeam}");
                role = await guild.CreateRoleAsync(pokemonTeam.ToString(), color: TeamRoleColors.GetColor(pokemonTeam));
            }

            return role;
        }

        async Task<TeamRolesDto> GetTeamRoles(IGuild guild)
        {
            var teams = Enum.GetNames(typeof(PokemonTeam));

            var mysticRole = await GetOrCreateRole(guild, PokemonTeam.Mystic);
            var instinctRole = await GetOrCreateRole(guild, PokemonTeam.Instinct);
            var valorRole = await GetOrCreateRole(guild, PokemonTeam.Valor);

            return new TeamRolesDto
            {
                RoleTeams = new Dictionary<ulong, PokemonTeam>
                {
                    [mysticRole.Id] = PokemonTeam.Mystic,
                    [instinctRole.Id] = PokemonTeam.Instinct,
                    [valorRole.Id] = PokemonTeam.Valor,
                },
                TeamRoles = new Dictionary<PokemonTeam, IRole>
                {
                    [PokemonTeam.Mystic] = mysticRole,
                    [PokemonTeam.Instinct] = instinctRole,
                    [PokemonTeam.Valor] = valorRole,
                }
            };
        }
    }
}
