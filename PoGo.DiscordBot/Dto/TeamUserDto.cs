using Discord;
using PoGo.DiscordBot.Configuration;

namespace PoGo.DiscordBot.Dto
{
    public class TeamUserDto
    {
        public IGuildUser User { get; set; }
        public PokemonTeam? Team { get; set; }

        public override string ToString() => User.Nickname ?? User.Username;
    }
}
