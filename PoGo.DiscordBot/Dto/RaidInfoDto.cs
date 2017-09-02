using Discord;
using PoGo.DiscordBot.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PoGo.DiscordBot.Dto
{
    public class RaidInfoDto
    {
        public const string TimeFormat = "H:mm";

        public DateTime CreatedAt { get; set; }
        public string BossName { get; set; }
        public string Location { get; set; }
        public DateTime Time { get; set; }
        public int? MinimumPlayers { get; set; }
        public IDictionary<ulong, PlayerDto> Players { get; set; } // <userId, PlayerDto>
        public List<(ulong UserId, int Count)> ExtraPlayers { get; set; }

        public bool IsExpired => Time < DateTime.Now;

        public RaidInfoDto()
        {
            CreatedAt = DateTime.UtcNow;
            Players = new Dictionary<ulong, PlayerDto>();
            MinimumPlayers = 4;
            ExtraPlayers = new List<(ulong UserId, int Count)>();
        }

        public string ToMessage() => $"Raid {BossName}, {Location}, {Time}";

        public Embed ToEmbed()
        {
            Color GetColor()
            {
                int playersCount = Players.Count + ExtraPlayers.Sum(t => t.Count);
                if (playersCount >= MinimumPlayers)
                    return Color.Green;
                if (playersCount >= MinimumPlayers / 2)
                    return Color.Orange;
                return Color.Red;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder
                .WithColor(GetColor())
                .AddInlineField("Boss", BossName)
                .AddInlineField("Kde", Location)
                .AddInlineField("Čas", Time.ToString(TimeFormat))
                ;

            if (Players.Any())
            {
                string playerFieldValue = Players.Count >= 10 ?
                    PlayersToGroupString(Players.Values) :
                    PlayersToString(Players.Values);

                embedBuilder.AddField($"Hráči ({Players.Count})", playerFieldValue);
            }

            if(ExtraPlayers.Any())
            {
                string extraPlayersFieldValue = string.Join(" + ", ExtraPlayers.Select(t => t.Count));
                embedBuilder.AddField($"Extra hráči ({ExtraPlayers.Sum(t => t.Count)})", extraPlayersFieldValue);
            }

            return embedBuilder.Build();
        }

        string PlayersToString(IEnumerable<PlayerDto> players) => string.Join(", ", players);

        string PlayersToGroupString(IEnumerable<PlayerDto> allPlayers)
        {
            string TeamToString(PokemonTeam? team) => team != null ? team.ToString() : "Bez teamu";

            List<string> formatterGroupedPlayers = new List<string>();

            var teams = new PokemonTeam?[] { PokemonTeam.Mystic, PokemonTeam.Instinct, PokemonTeam.Valor, null };
            foreach (PokemonTeam? team in teams)
            {
                var players = allPlayers.Where(t => t.Team == team).ToList();
                if (players.Any())
                    formatterGroupedPlayers.Add($"{TeamToString(team)} ({players.Count}) - {PlayersToString(players)}");
            }

            return string.Join(Environment.NewLine, formatterGroupedPlayers);
        }

        public static DateTime? ParseTime(string time)
        {
            var pieces = time.Split(' ', '.', ',', ':', ';', '\'');

            if (pieces.Length != 2 || !int.TryParse(pieces[0], out int hours) || !int.TryParse(pieces[1], out int minutes))
                return null;

            return DateTime.Now.Date.AddHours(hours).AddMinutes(minutes);
        }

        public static RaidInfoDto Parse(IUserMessage message)
        {
            var embed = message.Embeds.FirstOrDefault();
            if (embed == null || embed.Fields.Length < 3)
                return null;

            var time = ParseTime(embed.Fields[2].Value);
            if (!time.HasValue)
                return null;

            var result = new RaidInfoDto
            {
                CreatedAt = message.CreatedAt.UtcDateTime,
                BossName = embed.Fields[0].Value,
                Location = embed.Fields[1].Value,
                Time = time.Value,
            };

            return result;
        }
    }
}
