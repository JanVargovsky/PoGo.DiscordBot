using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class InfoModule : ModuleBase
    {
        [Command("info", RunMode = RunMode.Async)]
        [Alias("help")]
        public async Task WriteInfo()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithTitle("Příkazy")
            .AddField("Prefix", PoGoBot.Prefix)

            .AddField("Příkaz team",
$@"Přiřadí vám roli (a barvu) pro daný team.
Parametry příkazu: 'team' (Mystic | Valor | Instinct)
Použítí např.: {PoGoBot.Prefix}team Mystic")

            .AddField("Příkaz raid",
$@"Vytvoří anketu pro raid do speciálního kanálu 'raid-ankety'.
Parametry příkazu: 'boss' 'lokace' 'čas'
Použití např.: {PoGoBot.Prefix}raid Tyranitar Stoun 15:30
Pozn. Jestliže má jakýkoliv parametr mezery, je nutné ho obalit uvozovkami (""parametr s mezerou"")");

            var embed = embedBuilder.Build();
            await ReplyAsync(string.Empty, embed: embed);
        }
    }
}
