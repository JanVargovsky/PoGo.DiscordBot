namespace PoGo.DiscordBot.Dto
{
    public enum PokemonTeam
    {
        Mystic,
        Instinct,
        Valor
    }

    public static class PokemonTeamConverter
    {
        public static string ToUnicode(PokemonTeam team)
        {
            switch (team)
            {
                case PokemonTeam.Mystic:
                    return Emojis.Mystic;
                case PokemonTeam.Instinct:
                    return Emojis.Instinct;
                case PokemonTeam.Valor:
                    return Emojis.Valor;
                default:
                    return string.Empty;
            }
        }
    }
}
