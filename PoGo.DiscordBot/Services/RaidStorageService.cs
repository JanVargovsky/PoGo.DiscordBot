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
        private readonly RaidGuildMapping raidGuilds;

        public RaidStorageService()
        {
            raidGuilds = new RaidGuildMapping();
        }

        public void AddRaid(ulong guildId, ulong channelId, ulong messageId, RaidInfoDto raidInfoDto)
        {
            RaidChannelMapping raidChannels = raidGuilds.GuildRaids.GetOrAdd(guildId, _ => new RaidChannelMapping());
            RaidMessageMapping raidMessages = raidChannels.RaidChannels.GetOrAdd(channelId, _ => new RaidMessageMapping());
            raidMessages.RaidMessages[messageId] = raidInfoDto;
        }

        public RaidInfoDto GetRaid(ulong guildId, ulong channelId, int skip)
        {
            if (raidGuilds.GuildRaids.TryGetValue(guildId, out RaidChannelMapping raidChannels) &&
                raidChannels.RaidChannels.TryGetValue(channelId, out RaidMessageMapping raidMessages))
                return raidMessages.RaidMessages.Values
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip(skip)
                    .FirstOrDefault();

            return null;
        }

        public RaidInfoDto GetRaid(ulong guildId, ulong channelId, ulong messageId)
        {
            if (raidGuilds.GuildRaids.TryGetValue(guildId, out RaidChannelMapping raidChannels) &&
                raidChannels.RaidChannels.TryGetValue(channelId, out RaidMessageMapping raidMessages) &&
                raidMessages.RaidMessages.TryGetValue(messageId, out RaidInfoDto raidInfoDto))
                return raidInfoDto;

            return null;
        }

        public bool TryRemove(ulong guildId, ulong channelId, ulong messageId)
        {
            return raidGuilds.GuildRaids.TryGetValue(guildId, out RaidChannelMapping raidChannels) &&
raidChannels.RaidChannels.TryGetValue(channelId, out RaidMessageMapping raidMessages) &&
raidMessages.RaidMessages.TryRemove(messageId, out _);
        }

        public IEnumerable<(int Index, RaidInfoDto Raid)> GetActiveRaidsWithIndexes(ulong guildId, ulong channelId)
        {
            if (raidGuilds.GuildRaids.TryGetValue(guildId, out RaidChannelMapping raidChannels) &&
                raidChannels.RaidChannels.TryGetValue(channelId, out RaidMessageMapping raidMessages))
                return raidMessages.RaidMessages.Values
                    .OrderByDescending(t => t.CreatedAt)
                    .Select((t, i) => (i, t))
                    .Where(t => !t.t.IsExpired);

            return Enumerable.Empty<(int, RaidInfoDto)>();
        }

        public IEnumerable<(ulong guildId, ulong channelId, ulong messageId, RaidInfoDto raidInfo)> GetAll()
        {
            foreach (KeyValuePair<ulong, RaidChannelMapping> guild in raidGuilds.GuildRaids)
                foreach (KeyValuePair<ulong, RaidMessageMapping> channel in guild.Value.RaidChannels)
                    foreach (KeyValuePair<ulong, RaidInfoDto> raidMessage in channel.Value.RaidMessages)
                        yield return (guild.Key, channel.Key, raidMessage.Key, raidMessage.Value);
        }

        private class RaidGuildMapping
        {
            // <guildId, RaidChannels>
            public ConcurrentDictionary<ulong, RaidChannelMapping> GuildRaids { get; }

            public RaidGuildMapping()
            {
                GuildRaids = new ConcurrentDictionary<ulong, RaidChannelMapping>();
            }
        }

        private class RaidChannelMapping
        {
            // <channelId, RaidMessages>
            public ConcurrentDictionary<ulong, RaidMessageMapping> RaidChannels { get; }

            public RaidChannelMapping()
            {
                RaidChannels = new ConcurrentDictionary<ulong, RaidMessageMapping>();
            }
        }

        private class RaidMessageMapping
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