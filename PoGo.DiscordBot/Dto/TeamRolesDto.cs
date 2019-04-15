using Discord;
using PoGo.DiscordBot.Configuration;
using System.Collections.Generic;

namespace PoGo.DiscordBot.Dto
{
    public class TeamRolesDto
    {
        public IReadOnlyDictionary<ulong, PokemonTeam> RoleTeams { get; set; } // <RoleId, PokemonTeam>
        public IReadOnlyDictionary<PokemonTeam, IRole> TeamRoles { get; set; } // <PokemonTeam, Role>
    }
}