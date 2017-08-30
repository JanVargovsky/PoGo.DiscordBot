using Discord;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace PoGo.DiscordBot.Services
{
    public class RoleService
    {
        readonly IDiscordClient client;
        readonly ILogger<RoleService> logger;
        readonly Lazy<Task<IReadOnlyDictionary<PokemonTeam, IRole>>> TeamRolesLazy;
        public Task<IReadOnlyDictionary<PokemonTeam, IRole>> TeamRoles => TeamRolesLazy.Value;

        public RoleService(IDiscordClient client, ILogger<RoleService> logger)
        {
            this.client = client;
            this.logger = logger;
            TeamRolesLazy = new Lazy<Task<IReadOnlyDictionary<PokemonTeam, IRole>>>(async () =>
            {
                var guild = await client.GetGuildAsync(343037316752998410);
                var teams = Enum.GetNames(typeof(PokemonTeam));
                var result = new Dictionary<PokemonTeam, IRole>();

                foreach (var role in guild.Roles)
                    if (Enum.TryParse<PokemonTeam>(role.Name, out var team))
                        result[team] = role;

                return result;
            });
        }

        public Task OnUserJoined(SocketGuildUser user)
        {
            logger.LogInformation($"User joined {user.Id} '{user.Nickname ?? user.Username}'");
            return Task.CompletedTask;
        }
    }
}
