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

            IEnumerable<Discord.WebSocket.SocketGuildUser> users = Context.Guild.Users.Where(t => !t.IsBot);
            foreach (Discord.WebSocket.SocketGuildUser user in users)
            {
                PokemonTeam? team = userService.GetTeam(user);
                if (team != null)
                    groups[team.Value]++;
                else
                    withoutTeam++;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            foreach (KeyValuePair<PokemonTeam, int> item in groups)
                embedBuilder.AddInlineField(item.Key.ToString(), item.Value);
            if (withoutTeam != 0)
                embedBuilder.AddInlineField("Bez teamu", withoutTeam);

            await ReplyAsync(string.Empty, embed: embedBuilder.Build());
        }

        [Command("level")]
        [Alias("lvl")]
        [Summary("Vypíše informace o levelech hráčů.")]
        public async Task LevelStatistics()
        {
            List<Dto.PlayerDto> players = userService.GetPlayers(Context.Guild.Users)
                .Where(t => t?.Team != null && t?.Level != null)
                .ToList();

            var groupedPlayersPerTeam = players
                .GroupBy(t => t.Team.Value)
                .ToDictionary(t => t.Key, t => new
                {
                    Players = t.ToList(),
                    AverageLevel = t.Average(p => p.Level.Value),
                });

            double averageLevel = groupedPlayersPerTeam.Values.Average(t => t.AverageLevel);

            EmbedBuilder embedBuilder = new EmbedBuilder()
                .WithTitle("Průmerné levely");
            foreach (var team in groupedPlayersPerTeam)
                embedBuilder.AddInlineField($"{team.Key} ({team.Value.Players.Count})", $"{team.Value.AverageLevel:f2}");
            embedBuilder.AddField($"Všichni ({players.Count})", $"{averageLevel:f2}");

            await ReplyAsync(string.Empty, embed: embedBuilder.Build());
        }
    }
}