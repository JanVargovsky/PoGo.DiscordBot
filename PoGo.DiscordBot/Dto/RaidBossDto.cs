using System.Collections.Generic;

namespace PoGo.DiscordBot.Dto
{
    public class RaidBossDto
    {
        public string BossName { get; set; }
        public string[] Type { get; set; }
        public Dictionary<string, string> CPs { get; set; }
        public string[] Weakness { get; set; }
        public string[] ChargeAttacks { get; set; }
        public string[] Counters { get; set; }
    }
}