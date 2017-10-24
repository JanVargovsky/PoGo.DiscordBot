using Discord;
using Discord.Commands;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using System;
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
        [Obsolete]
        public async Task WriteInfo()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithTitle("Příkazy")
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

        [Command("contribute", RunMode = RunMode.Async)]
        [Alias("github")]
        public async Task Contribute()
        {
            await ReplyAsync("https://github.com/JanVargovsky/PoGo.DiscordBot");
        }

        [Command("donate", RunMode = RunMode.Async)]
        public async Task Donate()
        {
            await ReplyAsync("V případě, že byste chtěli podpořit vývoj, tak se ozvěte Pako#3904");
        }
    }
}
