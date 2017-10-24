﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration.Options;
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

            public RaidChannelBinding(ITextChannel from, ITextChannel to)
            {
                From = from;
                To = to;
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
                AddBindingIfValid(channelBindings, guild, channel.From, channel.To);
        }

        public bool IsKnown(ulong guildId) => guilds.ContainsKey(guildId);

        public bool IsKnown(ulong guildId, ulong textChannelId) =>
            TryGetRaidChannel(guildId, textChannelId) != null;

        public IEnumerable<ITextChannel> GetRaidChannels(ulong guildId) => guilds[guildId].Select(t => t.To);

        /// <summary>
        /// Returns raid channel for the raid poll based on the channel where the command came from.
        /// </summary>
        public ITextChannel TryGetRaidChannel(ulong guildId, ulong fromTextChannelId)
        {
            if (guilds.TryGetValue(guildId, out var raidChannelBindings))
                foreach (var channel in raidChannelBindings)
                    if (channel.From == null || channel.From.Id == fromTextChannelId)
                        return channel.To;

            return null;
        }

        void AddBindingIfValid(List<RaidChannelBinding> channelBindings, SocketGuild guild, string from, string to)
        {
            var channelFrom = guild.TextChannels.FirstOrDefault(t => t.Name == from);
            var channelTo = guild.TextChannels.FirstOrDefault(t => t.Name == to);

            if (from != "*" && (channelFrom == null || channelTo == null))
            {
                if (channelFrom == null)
                    logger.LogError($"Unknown from channel binding '{from}'");
                if (channelTo == null)
                    logger.LogError($"Unknown to channel binding '{to}'");
                return;
            }

            channelBindings.Add(new RaidChannelBinding(channelFrom, channelTo));
        }

        public void AddBinding(SocketGuild guild, string from, string to) => AddBindingIfValid(guilds[guild.Id], guild, from, to);
    }
}
