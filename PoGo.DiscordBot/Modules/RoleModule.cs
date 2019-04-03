using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    [Group("role")]
    public class RoleModule : ModuleBase<SocketCommandContext>
    {
        readonly ILogger<RoleModule> logger;
        readonly RoleService roleService;
        readonly Dictionary<ulong, string[]> availableRoles; // <guildId, roles[]>

        public RoleModule(ILogger<RoleModule> logger, IOptions<ConfigurationOptions> options, RoleService roleService)
        {
            this.logger = logger;
            this.roleService = roleService;
            availableRoles = options.Value.Guilds
                .Where(t => t.FreeRoles != null)
                .ToDictionary(t => t.Id, t => t.FreeRoles);
        }

        [Command("add")]
        [Alias("a")]
        [Summary("AddRoleSummary")]
        public async Task AddRole([Summary("Název role")]string roleName)
        {
            if (!(Context.User is SocketGuildUser user))
                return;

            if (!availableRoles.TryGetValue(Context.Guild.Id, out var roles) || !roles.Contains(roleName))
                return;

            var role = roleService.GetRoleByName(Context.Guild, roleName);
            if (role == null)
            {
                await ReplyAsync("Neznámá role.");
                return;
            }

            await user.AddRoleAsync(role);
            await ReplyAsync($"Byla ti přidáná role '{roleName}'");
        }

        [Command("remove")]
        [Alias("r")]
        [Summary("Smaže uživateli roli.")]
        public async Task RemoveRole([Summary("Název role")]string roleName)
        {
            if (!(Context.User is SocketGuildUser user))
                return;

            if (!availableRoles.TryGetValue(Context.Guild.Id, out var roles) || !roles.Contains(roleName))
                return;

            var role = roleService.GetRoleByName(Context.Guild, roleName);
            if (role == null)
            {
                await ReplyAsync("Neznámá role.");
                return;
            }

            await user.RemoveRoleAsync(role);
            await ReplyAsync($"Byla ti odebrána role '{roleName}'");
        }
    }
}
