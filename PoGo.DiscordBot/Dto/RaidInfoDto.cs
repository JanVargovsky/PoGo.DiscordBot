using Discord;
using Microsoft.Extensions.Localization;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;

namespace PoGo.DiscordBot.Dto
{
    public enum RaidType
    {
        Normal, // Today - within a few hours
        Scheduled,
    }

    public class RaidInfoDto
    {
        public const string TimeFormat = "H:mm";
        public const string DateTimeFormat = "d.M.yyyy H:mm";

        public readonly static string DateWord = LocalizationService.Instance.GetStringFromResources("Date");

        public readonly static string TimeWord = LocalizationService.Instance.GetStringFromResources("Time");

        public IUserMessage Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BossName { get; set; }
        public string Location { get; set; }
        public DateTime DateTime { get; set; }
        public IDictionary<ulong, PlayerDto> Players { get; set; } // <userId, PlayerDto>
        public List<(ulong UserId, int Count)> ExtraPlayers { get; set; }
        public bool IsExpired => DateTime < DateTime.Now;
        public RaidType RaidType { get; set; }

        string DateTimeAsString => DateTime.ToString(RaidType == RaidType.Normal ? TimeFormat : DateTimeFormat);

        public RaidInfoDto(RaidType raidType)
        {
            RaidType = raidType;
            CreatedAt = DateTime.UtcNow;
            Players = new Dictionary<ulong, PlayerDto>();
            ExtraPlayers = new List<(ulong UserId, int Count)>();
        }

        public Embed ToEmbed()
        {
            Color GetColor()
            {
                if (RaidType == RaidType.Scheduled)
                {
                    return !IsExpired ? new Color(191, 155, 48) : Color.Red;
                }

                var remainingTime = DateTime - DateTime.Now;

                if (remainingTime.TotalMinutes <= 0)
                    return Color.Red;
                if (remainingTime.TotalMinutes <= 15)
                    return Color.Orange;
                return Color.Green;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder
                .WithColor(GetColor())
                .AddInlineField("Boss", BossName)
                .AddInlineField(LocalizationService.Instance.GetStringFromResources("Where"), Location)
                .AddInlineField(RaidType == RaidType.Normal ? TimeWord : DateWord, DateTimeAsString)
                ;

            if (Players.Count > 0)
            {
                string playerFieldValue = Players.Count >= 10 ?
                    PlayersToGroupString(Players.Values) :
                    PlayersToString(Players.Values);

                embedBuilder.AddField(LocalizationService.Instance.GetStringFromResources("Players") + $"({Players.Count})", playerFieldValue);
            }

            if (ExtraPlayers.Count > 0)
            {
                string extraPlayersFieldValue = string.Join(" + ", ExtraPlayers.Select(t => t.Count));
                embedBuilder.AddField(LocalizationService.Instance.GetStringFromResources("OtherPlayers") +  $"({ExtraPlayers.Sum(t => t.Count)})", extraPlayersFieldValue);
            }

            return embedBuilder.Build();
        }

        public string ToSimpleString() => $"{BossName} {Location} {DateTimeAsString}";

        string PlayersToString(IEnumerable<PlayerDto> players) => string.Join(", ", players);

        string PlayersToGroupString(IEnumerable<PlayerDto> allPlayers)
        {
            string TeamToString(PokemonTeam? team) => team != null ? team.ToString() : LocalizationService.Instance.GetStringFromResources("WithoutTeam");

            List<string> formatterGroupedPlayers = new List<string>();

            var teams = new PokemonTeam?[] { PokemonTeam.Mystic, PokemonTeam.Instinct, PokemonTeam.Valor, null };
            foreach (PokemonTeam? team in teams)
            {
                var players = allPlayers.Where(t => t.Team == team).ToList();
                if (players.Count > 0)
                    formatterGroupedPlayers.Add($"{TeamToString(team)} ({players.Count}) - {PlayersToString(players)}");
            }

            return string.Join(Environment.NewLine, formatterGroupedPlayers);
        }

        public static DateTime? ParseTime(string time) => ParseTime(time, DateTime.Now.Date);

        public static DateTime? ParseTime(string time, DateTime date)
        {
            var pieces = time.Split(' ', '.', ',', ':', ';', '\'');

            if (pieces.Length != 2 || !int.TryParse(pieces[0], out int hours) || !int.TryParse(pieces[1], out int minutes))
                return null;

            return new DateTime(date.Year, date.Month, date.Day, hours, minutes, 0);
        }

        public static DateTime? ParseDateTime(string dateTime)
        {
            DateTime? result = null;
            try
            {
                var tokens = dateTime.Split(new[] { ' ', '.', ',', ':', ';', '\'', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 5)
                    throw new Exception($"Invalid date '{dateTime}'");
                var intTokens = tokens.Select(int.Parse).ToArray();

                result = new DateTime(intTokens[2], intTokens[1], intTokens[0], intTokens[3], intTokens[4], 0);
            }
            catch
            {
            }
            return result;
        }

        public static RaidInfoDto Parse(IUserMessage message)
        {
            var embed = message.Embeds.FirstOrDefault();
            if (embed == null || embed.Fields.Length < 3)
                return null;

            RaidInfoDto result = null;

            if (embed.Fields[2].Name.Equals(TimeWord,StringComparison.OrdinalIgnoreCase))
            {
                var time = ParseTime(embed.Fields[2].Value, message.CreatedAt.Date);
                if (!time.HasValue)
                    return null;

                result = new RaidInfoDto(RaidType.Normal)
                {
                    Message = message,
                    CreatedAt = message.CreatedAt.UtcDateTime,
                    BossName = embed.Fields[0].Value,
                    Location = embed.Fields[1].Value,
                    DateTime = time.Value,
                };
            }
            else if (embed.Fields[2].Name.Equals(DateWord,StringComparison.OrdinalIgnoreCase))
            {
                var dateTime = ParseDateTime(embed.Fields[2].Value);
                if (!dateTime.HasValue)
                    return null;

                result = new RaidInfoDto(RaidType.Scheduled)
                {
                    Message = message,
                    CreatedAt = message.CreatedAt.UtcDateTime,
                    BossName = embed.Fields[0].Value,
                    Location = embed.Fields[1].Value,
                    DateTime = dateTime.Value,
                };
            }

            return result;
        }
    }
}
