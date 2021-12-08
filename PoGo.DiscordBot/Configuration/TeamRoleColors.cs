using Discord;

namespace PoGo.DiscordBot.Configuration;

public static class TeamRoleColors
{
    public static Color ToColor(this PokemonTeam team) => team switch
    {
        PokemonTeam.Mystic => new(0x00, 0xb8, 0xff),
        PokemonTeam.Instinct => new(0xff, 0xf5, 0x00),
        PokemonTeam.Valor => new(0xff, 0x19, 0x05),
        _ => throw new("Unknown team"),
    };
}
