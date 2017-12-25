using Discord;

namespace PoGo.DiscordBot.Dto
{
    public class RaidChannelBindingDto
    {
        public ITextChannel Channel { get; set; }
        public IMentionable Mention { get; set; }
        public bool AllowScheduledRaids { get; set; }
    }
}
