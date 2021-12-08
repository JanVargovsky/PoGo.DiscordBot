using System.Collections.Generic;
using Discord;
using PoGo.DiscordBot.Configuration;

namespace PoGo.DiscordBot.Dto;

public class TeamRolesDto
{
    public IReadOnlyDictionary<ulong, PokemonTeam> RoleTeams { get; set; } // <RoleId, PokemonTeam>
    public IReadOnlyDictionary<PokemonTeam, IRole> TeamRoles { get; set; } // <PokemonTeam, Role>
}
