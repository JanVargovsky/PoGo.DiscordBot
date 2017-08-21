using Discord;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class RoleService
    {
        private readonly IDiscordClient client;

        readonly Lazy<Task<IReadOnlyDictionary<PokemonTeam, IRole>>> TeamRolesLazy;
        public Task<IReadOnlyDictionary<PokemonTeam, IRole>> TeamRoles => TeamRolesLazy.Value;

        public RoleService(IDiscordClient client)
        {
            this.client = client;
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
    }
}
