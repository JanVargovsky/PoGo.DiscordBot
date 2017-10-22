using Discord;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoGo.DiscordBot.Services
{
    class RaidChannelService
    {
        readonly Dictionary<ulong, GuildOptions> guilds;

        public RaidChannelService(IOptions<ConfigurationOptions> configurationOptions)
        {
            guilds = configurationOptions.Value.Guilds
                .ToDictionary(t => t.Id, t => t);
        }

        public bool IsKnown(ulong guildId) => guilds.ContainsKey(guildId);

        //public ITextChannel TryGetRaidChannels
    }
}
