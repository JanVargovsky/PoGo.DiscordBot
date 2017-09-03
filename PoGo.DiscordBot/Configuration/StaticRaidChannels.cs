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
                [353860813364396032] = 353860981669232641, // PoGo Bot Dev - #raids
                [343037316752998410] = 348844165741936641, // PoGo Mapa FM - #raid-diskuze
            });
        }
    }
}
