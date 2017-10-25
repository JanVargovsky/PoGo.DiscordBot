using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Dto;
using System.Collections.Generic;
using System.Linq;

namespace PoGo.DiscordBot.Services
{
    public class RaidChannelService
    {
        class RaidChannelBinding
        {
            public ITextChannel From { get; }
            public ITextChannel To { get; }
            public IMentionable Mention { get; set; }

            public RaidChannelBinding(ITextChannel from, ITextChannel to, IMentionable mention)
            {
                From = from;
                To = to;
                Mention = mention;
            }
        }

        readonly Dictionary<ulong, List<RaidChannelBinding>> guilds; // <guildId, channels[]>
        private readonly ILogger<RaidChannelService> logger;

        public RaidChannelService(ILogger<RaidChannelService> logger)
        {
            this.logger = logger;
            guilds = new Dictionary<ulong, List<RaidChannelBinding>>();
        }

        public void OnNewGuild(SocketGuild guild, GuildOptions[] guildOptions)
        {
            var guildConfig = guildOptions.FirstOrDefault(t => t.Id == guild.Id);
            if (guildConfig == null)
            {
                logger.LogWarning($"Unknown guild with id '{guild.Id}', name '{guild.Name}'");
                return;
            }

            if (guildConfig.Channels == null)
            {
                logger.LogError($"Guild with custom name '{guildConfig.Name}' does not have configured channels");
                return;
            }

            var channelBindings = guilds[guild.Id] = new List<RaidChannelBinding>();

            // go through configured channels and register them
            foreach (var channel in guildConfig.Channels)
                AddBindingIfValid(channelBindings, guild, channel);
        }

        public bool IsKnown(ulong guildId) => guilds.ContainsKey(guildId);

        public bool IsKnown(ulong guildId, ulong textChannelId) =>
            TryGetRaidChannelBinding(guildId, textChannelId) != null;

        public IEnumerable<ITextChannel> GetRaidChannels(ulong guildId) => guilds[guildId].Select(t => t.To);

        /// <summary>
        /// Returns raid channel for the raid poll based on the channel where the command came from.
        /// </summary>
        public RaidChannelBindingDto TryGetRaidChannelBinding(ulong guildId, ulong fromTextChannelId)
        {
            if (guilds.TryGetValue(guildId, out var raidChannelBindings))
                foreach (var channel in raidChannelBindings)
                    if (channel.From == null || channel.From.Id == fromTextChannelId)
                        return new RaidChannelBindingDto
                        {
                            Channel = channel.To,
                            Mention = channel.Mention,
                        };

            return null;
        }

        void AddBindingIfValid(List<RaidChannelBinding> channelBindings, SocketGuild guild, ChannelOptions channelOptions)
        {
            var channelFrom = guild.TextChannels.FirstOrDefault(t => t.Name == channelOptions.From);
            var channelTo = guild.TextChannels.FirstOrDefault(t => t.Name == channelOptions.To);

            bool channelFromBinding = channelFrom == null && channelOptions.From != "*";
            bool channelToBinding = channelTo == null;
            if (channelFromBinding || channelToBinding)
            {
                if (channelFromBinding)
                    logger.LogError($"Unknown from channel binding '{channelOptions.From}'");
                if (channelToBinding)
                    logger.LogError($"Unknown to channel binding '{channelOptions.To}'");
                return;
            }

            IMentionable mention = null;
            if (!string.IsNullOrEmpty(channelOptions.Mention))
            {
                mention = guild.Roles.FirstOrDefault(t => t.Name == channelOptions.Mention);
                if (mention == null)
                    logger.LogError($"Unknown role '{channelOptions.Mention}'");
            }

            channelBindings.Add(new RaidChannelBinding(channelFrom, channelTo, mention));
        }
    }
}
