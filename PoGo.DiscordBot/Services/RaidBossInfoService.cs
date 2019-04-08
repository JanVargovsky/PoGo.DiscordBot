using Discord.WebSocket;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Dto;
using System.Collections.Generic;
using System.Linq;

namespace PoGo.DiscordBot.Services
{
    public class RaidBossInfoService
    {
        readonly Dictionary<string, RaidBossDto> raidBosses; // <bossName, dto>

        public RaidBossInfoService(IOptions<ConfigurationOptions> options)
        {
            raidBosses = options.Value.RaidBosses.ToDictionary(t => t.Key.ToLower(), t => new RaidBossDto
            {
                BossName = t.Key,
                Type = t.Value.Type,
                CPs = t.Value.CP,
                Weakness = t.Value.Weakness,
                ChargeAttacks = t.Value.ChargeAttacks,
                Counters = t.Value.Counters,
            });
        }

        public IEnumerable<string> GetAllKnownBossNames() => raidBosses.Values
             .Select(t => t.BossName)
             .OrderBy(t => t);

        public RaidBossDto GetBoss(string bossName) =>
            raidBosses.TryGetValue(bossName.ToLower(), out var dto) ? dto : null;

        public string GetBossNameWithEmoji(string bossName, SocketGuild guild)
        {
            var emote = guild.Emotes.FirstOrDefault(t => string.Compare(t.Name, bossName, true) == 0);

            if (emote != null)
                return $"{bossName} {emote}";
            else
                return bossName;
        }
    }
}