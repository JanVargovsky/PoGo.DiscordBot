using System.Collections.Generic;

namespace PoGo.DiscordBot.Configuration.Options
{
    public class ConfigurationOptions
    {
        public char Prefix { get; set; }
        public string Token { get; set; }
        public GuildOptions[] Guilds { get; set; }
        public Dictionary<string, RaidBossOptions> RaidBosses { get; set; }
    }

    public class GuildOptions
    {
        public string Name { get; set; }
        public ulong Id { get; set; }
        public bool IgnoreMention { get; set; }
        public string[] FreeRoles { get; set; }
        public ChannelOptions[] Channels { get; set; }
        public List<GymInfoOptions> Gyms { get; set; }
    }

    public class ChannelOptions
    {
        public string Mention { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public bool ScheduledRaids { get; set; }
    }

    public class RaidBossOptions
    {
        public string[] Type { get; set; }
        public Dictionary<string, string> CP { get; set; }
        public string[] Weakness { get; set; }
        public string[] ChargeAttacks { get; set; }
        public string[] Counters { get; set; }
    }

    public class GymInfoOptions
    {
        public string Name { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
    }
}
