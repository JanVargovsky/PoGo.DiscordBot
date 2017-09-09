using Discord;
using Discord.Commands;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    [Group("stats")]
    public class StatisticsModule : ModuleBase<SocketCommandContext>
    {
        private readonly UserService userService;

        public StatisticsModule(UserService userService)
        {
            this.userService = userService;
        }

        [Command("team", RunMode = RunMode.Async)]
        [Summary("Vypíše počet lidí ve všech týmech.")]
        public async Task TeamsStatistics()
        {
            Dictionary<PokemonTeam, int> groups = new Dictionary<PokemonTeam, int>
            {
                [PokemonTeam.Mystic] = 0,
                [PokemonTeam.Instinct] = 0,
                [PokemonTeam.Valor] = 0,
            };
            int withoutTeam = 0;

            var users = Context.Guild.Users.Where(t => !t.IsBot);
            foreach (var user in users)
            {
                var team = userService.GetTeam(user);
                if (team != null)
                    groups[team.Value]++;
                else
                    withoutTeam++;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            foreach (var item in groups)
                embedBuilder.AddInlineField(item.Key.ToString(), item.Value);
            if(withoutTeam != 0)
                embedBuilder.AddInlineField("Bez teamu", withoutTeam);

            await ReplyAsync(string.Empty, embed: embedBuilder.Build());
        }

        [RequireOwner] // Not done yet
        [Command("level")]
        [Summary("Vypíše průměrný level všech hráčů.")]
        public async Task LevelStatistics()
        {
            var allPlayers = Context.Guild.Users
                .Where(t => !t.IsBot)
                .Select(t => userService.GetPlayerLevel(t))
                .ToList();

            var playerLevels = allPlayers
                .Where(t => t.HasValue)
                .Select(t => t.Value)
                .ToList();

            int totalPlayers = allPlayers.Count;
            double averageLevel = playerLevels.Average();

            var embedBuilder = new EmbedBuilder();

            await ReplyAsync(string.Empty, embed: embedBuilder.Build());
        }
    }
}
