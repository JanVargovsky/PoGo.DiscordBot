using Discord;
using Discord.Commands;
using PoGo.DiscordBot.Services;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.Administrator)]
    public class TestModule : ModuleBase
    {
        private readonly RoleService roleService;

        public TestModule(RoleService roleService)
        {
            this.roleService = roleService;
        }

        [Command("test")]
        [Alias("t")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Test()
        {
            var roles = await roleService.TeamRoles;
            var mention = string.Join(' ', roles.Values.Select(t => t.Mention));
            await ReplyAsync($"Dostal každý notifikaci? {mention}");
        }
    }
}
