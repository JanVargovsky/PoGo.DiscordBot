using Discord;
using Discord.Commands;
using PoGo.DiscordBot.Services;
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
            await Task.CompletedTask;
            //await ReplyAsync("Raid", embed: );
        }
    }
}
