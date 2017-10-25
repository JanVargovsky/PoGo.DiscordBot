namespace PoGo.DiscordBot.Configuration.Options
{
    public class ConfigurationOptions
    {
        public char Prefix { get; set; }
        public string Token { get; set; }
        public GuildOptions[] Guilds { get; set; }
    }

    public class GuildOptions
    {
        public string Name { get; set; }
        public ulong Id { get; set; }
        public bool IgnoreMention { get; set; }
        public ChannelOptions[] Channels { get; set; }
    }

    public class ChannelOptions
    {
        public string Mention { get; set; }
        public string From { get; set; }
        public string To { get; set; }
    }
}
