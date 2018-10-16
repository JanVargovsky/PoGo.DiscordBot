using PoGo.DiscordBot.Dto;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PoGo.DiscordBot.Services
{
    public class RaidStorageService
    {
        // <guildId, <channelId, <messageId, RaidInfo>>>
        //readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, RaidInfoDto>>> raids;
        readonly RaidGuildMapping raidGuilds;

        public RaidStorageService()
        {
            raidGuilds = new RaidGuildMapping();
        }

        public void AddRaid(ulong guildId, ulong channelId, ulong messageId, RaidInfoDto raidInfoDto)
        {
            var raidChannels = raidGuilds.GuildRaids.GetOrAdd(guildId, _ => new RaidChannelMapping());
            var raidMessages = raidChannels.RaidChannels.GetOrAdd(channelId, _ => new RaidMessageMapping());
            raidMessages.RaidMessages[messageId] = raidInfoDto;
        }

        public RaidInfoDto GetRaid(ulong guildId, ulong channelId, int skip)
        {
            if (raidGuilds.GuildRaids.TryGetValue(guildId, out var raidChannels) &&
                raidChannels.RaidChannels.TryGetValue(channelId, out var raidMessages))
                return raidMessages.RaidMessages.Values
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip(skip)
                    .FirstOrDefault();

            return null;
        }

        public RaidInfoDto GetRaid(ulong guildId, ulong channelId, ulong messageId)
        {
            if (raidGuilds.GuildRaids.TryGetValue(guildId, out var raidChannels) &&
                raidChannels.RaidChannels.TryGetValue(channelId, out var raidMessages) &&
                raidMessages.RaidMessages.TryGetValue(messageId, out var raidInfoDto))
                return raidInfoDto;

            return null;
        }

        public bool TryRemove(ulong guildId, ulong channelId, ulong messageId) =>
            raidGuilds.GuildRaids.TryGetValue(guildId, out var raidChannels) &&
            raidChannels.RaidChannels.TryGetValue(channelId, out var raidMessages) &&
            raidMessages.RaidMessages.TryRemove(messageId, out _);

        public IEnumerable<(int Index, RaidInfoDto Raid)> GetActiveRaidsWithIndexes(ulong guildId, ulong channelId)
        {
            if (raidGuilds.GuildRaids.TryGetValue(guildId, out var raidChannels) &&
                raidChannels.RaidChannels.TryGetValue(channelId, out var raidMessages))
                return raidMessages.RaidMessages.Values
                    .OrderByDescending(t => t.CreatedAt)
                    .Select((t, i) => (i, t))
                    .Where(t => !t.t.IsExpired);

            return Enumerable.Empty<(int, RaidInfoDto)>();
        }

        public IEnumerable<(ulong guildId, ulong channelId, ulong messageId, RaidInfoDto raidInfo)> GetAll()
        {
            foreach (var guild in raidGuilds.GuildRaids)
                foreach (var channel in guild.Value.RaidChannels)
                    foreach (var raidMessage in channel.Value.RaidMessages)
                        yield return (guild.Key, channel.Key, raidMessage.Key, raidMessage.Value);
        }

        class RaidGuildMapping
        {
            // <guildId, RaidChannels>
            public ConcurrentDictionary<ulong, RaidChannelMapping> GuildRaids { get; }

            public RaidGuildMapping()
            {
                GuildRaids = new ConcurrentDictionary<ulong, RaidChannelMapping>();
            }
        }

        class RaidChannelMapping
        {
            // <channelId, RaidMessages>
            public ConcurrentDictionary<ulong, RaidMessageMapping> RaidChannels { get; }

            public RaidChannelMapping()
            {
                RaidChannels = new ConcurrentDictionary<ulong, RaidMessageMapping>();
            }
        }

        class RaidMessageMapping
        {
            // <messageId, RaidInfo>
            public ConcurrentDictionary<ulong, RaidInfoDto> RaidMessages { get; }

            public RaidMessageMapping()
            {
                RaidMessages = new ConcurrentDictionary<ulong, RaidInfoDto>();
            }
        }
    }
}
