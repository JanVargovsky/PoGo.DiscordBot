using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.Logging;

namespace PoGo.DiscordBot.Modules;

[RequireOwner]
public class TestModule : ModuleBase<SocketCommandContext>
{
    private readonly ILogger<TestModule> logger;

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
