using Discord;
using Discord.Commands;
using PoGo.DiscordBot.Services;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.Administrator)]
    public class TestModule : ModuleBase<SocketCommandContext>
    {
        [Command("test")]
        [Alias("t")]
        public async Task Test()
        {
            await Task.CompletedTask;
            //await ReplyAsync("Raid", embed: );
        }
    }
}
