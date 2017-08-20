using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.Administrator)]
    public class TestModule : ModuleBase
    {
        public async Task Test()
        {
            await Task.CompletedTask;
        }
    }
}
