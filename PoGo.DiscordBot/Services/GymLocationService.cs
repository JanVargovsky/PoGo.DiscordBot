using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Common;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Dto;

namespace PoGo.DiscordBot.Services;

public class GymLocationService
{
    readonly Dictionary<ulong, (string NormalizedName, GymInfoDto GymInfo)[]> gymsInfos; // <guildId, gymInfo[]>

    public GymLocationService(IOptions<ConfigurationOptions> options)
    {
        gymsInfos = options.Value.Guilds
            .Where(t => t.Gyms?.Any() == true)
            .ToDictionary(t => t.Id, t => t.Gyms.Select(g => (StringUtils.ToLowerWithoutDiacritics(g.Name), new GymInfoDto
            {
                Name = g.Name,
                Latitude = g.Latitude,
                Longitude = g.Longitude
            })).ToArray());
    }

    public IEnumerable<GymInfoDto> Search(ulong guildId, string name)
    {
        if (!gymsInfos.TryGetValue(guildId, out var gyms))
            return null;

        var normalizedName = StringUtils.ToLowerWithoutDiacritics(name);
        return gyms
            .Where(t => t.NormalizedName.Contains(normalizedName))
            .Select(t => t.GymInfo);
    }

    public string GetMapUrl(GymInfoDto gymInfo) => $"http://maps.google.com/maps?q={gymInfo.Latitude},{gymInfo.Longitude}";
}
