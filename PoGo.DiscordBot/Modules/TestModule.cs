using Discord.Commands;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireOwner]
    public class TestModule : ModuleBase<SocketCommandContext>
    {
        readonly ILogger<TestModule> logger;

        public TestModule(ILogger<TestModule> logger)
        {
            this.logger = logger;
        }

        [Command("test")]
        [Alias("t")]
        [Summary("Test.")]
        public async Task Test(string a, string b)
        {
            await Task.CompletedTask;
        }
    }
}