using Discord;
using System;

namespace PoGo.DiscordBot.Configuration
{
    public class TeamRoleColors
    {
        public static Color GetColor(PokemonTeam team)
        {
            switch (team)
            {
                case PokemonTeam.Mystic:
                    return new Color(0x00, 0xb8, 0xff);
                case PokemonTeam.Instinct:
                    return new Color(0x00, 0xb8, 0xff);
                case PokemonTeam.Valor:
                    return new Color(0x00, 0xb8, 0xff);
                default:
                    throw new Exception("Unknown team");
            }
        }
    }
}
