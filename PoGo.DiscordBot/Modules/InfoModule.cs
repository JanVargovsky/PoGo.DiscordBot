using Discord;
using Discord.Commands;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class InfoModule : ModuleBase
    {
        private readonly ConfigurationOptions configuration;

        public InfoModule(IOptions<ConfigurationOptions> configurationOptionsAccessor)
        {
            configuration = configurationOptionsAccessor.Value;
        }

        [Command("info", RunMode = RunMode.Async)]
        [Alias("help")]
        public async Task WriteInfo()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithTitle("Příkazy")
            .AddField("Prefix", configuration.Prefix)

            .AddField("Příkaz team",
$@"Přiřadí vám roli (a barvu) pro daný team.
Parametry příkazu: team (Mystic | Valor | Instinct)
Použítí např.: {configuration.Prefix}team Mystic")

            .AddField("Příkaz raid",
$@"Vytvoří anketu pro raid do speciálního kanálu #raid-ankety.
Parametry příkazu: boss lokace čas [počet lidí]
Použití např.:
{configuration.Prefix}raid Tyranitar Stoun 15:30
{configuration.Prefix}raid Lugia ""Židovský hřbitov"" 16:00
{configuration.Prefix}raid Machamp Žirafa 12:00 2
Pozn. Jestliže má jakýkoliv parametr mezery, je nutné ho obalit uvozovkami (""parametr s mezerou"")");

            var embed = embedBuilder.Build();
            await ReplyAsync(string.Empty, embed: embed);
        }
    }
}
