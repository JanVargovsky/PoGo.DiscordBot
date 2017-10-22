using Discord.Commands;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireOwner]
    public class TestModule : ModuleBase<SocketCommandContext>
    {
        [Command("test")]
        [Alias("t")]
        [Summary("Test.")]
        public async Task Test()
        {
            await Task.CompletedTask;
        }
    }
}
