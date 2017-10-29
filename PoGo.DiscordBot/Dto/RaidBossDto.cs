namespace PoGo.DiscordBot.Dto
{
    public class RaidBossDto
    {
        public string BossName { get; set; }
        public int MinCP { get; set; }
        public int MaxCP { get; set; }
        public string[] Counters { get; set; }
    }
}
