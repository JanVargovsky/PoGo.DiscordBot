using Discord.Commands;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Modules.Preconditions;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireOwner]
    public class TestModule : ModuleBase<SocketCommandContext>
    {
        [Command("test")]
        [Alias("t")]
        [Summary("Test.")]
        [TeamPrecondition]
        public async Task Test(
            [Summary("Test description for param.")]string param,
            [Summary("Test pokemon team description for param team.")]PokemonTeam team)
        {
            await Task.CompletedTask;
        }
    }
}
