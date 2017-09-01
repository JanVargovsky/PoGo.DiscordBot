using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PoGo.DiscordBot.Configuration
{
    public class StaticRaidChannels
    {
        public IReadOnlyDictionary<ulong, ulong> GuildToTextChannelBinding { get; }

        public StaticRaidChannels()
        {
            GuildToTextChannelBinding = new ReadOnlyDictionary<ulong, ulong>(new Dictionary<ulong, ulong>()
            {
                [343037316752998410] = 348844165741936641, // PoGo Mapa FM - #raid-diskuze
            });
        }
    }
}
