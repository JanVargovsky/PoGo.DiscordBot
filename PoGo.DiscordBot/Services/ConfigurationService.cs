using System.Linq;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;

namespace PoGo.DiscordBot.Services
{
    public class ConfigurationService
    {
        readonly ConfigurationOptions configurationOptions;

        public ConfigurationService(IOptions<ConfigurationOptions> configurationOptions)
        {
            this.configurationOptions = configurationOptions.Value;
        }

        public GuildOptions GetGuildOptions(ulong guildId) => configurationOptions.Guilds.FirstOrDefault(t => t.Id == guildId);
    }
}
