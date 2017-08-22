using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PoGo.DiscordBot.Services
{
    public class StaticRaidChannels
    {
        public IReadOnlyDictionary<ulong, ulong> StaticGuildToTextMessageBinding;

        public StaticRaidChannels()
        {
            StaticGuildToTextMessageBinding = new ReadOnlyDictionary<ulong, ulong>(new Dictionary<ulong, ulong>()
            {
                [343037316752998410] = 348844165741936641, // PoGo Mapa FM - #raid-diskuze
            });
        }
    }
}
