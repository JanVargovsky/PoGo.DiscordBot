using Discord;
using PoGo.DiscordBot.Configuration.Options;

namespace PoGo.DiscordBot.Dto
{
    public class RaidChannelBindingDto
    {
        public ITextChannel Channel { get; set; }
        public IMentionable Mention { get; set; }
        public RaidChannelType RaidChannelType { get; set; }
    }
}
