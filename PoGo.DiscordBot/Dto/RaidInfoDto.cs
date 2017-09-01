using Discord;
using PoGo.DiscordBot.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PoGo.DiscordBot.Dto
{
    public class RaidInfoDto
    {
        public const string TimeFormat = "H:mm";

        public ulong CreatedByUserId { get; set; }
        public ulong MessageId { get; set; }
        public DateTime Created { get; set; }
        public string BossName { get; set; }
        public string Location { get; set; }
        public DateTime Time { get; set; }
        public int? MinimumPlayers { get; set; }
        public IDictionary<ulong, TeamUserDto> Users { get; set; } // <userId, IGuildUser>

        public bool IsActive => Created.AddHours(3) >= DateTime.UtcNow;

        public RaidInfoDto()
        {
            Users = new Dictionary<ulong, TeamUserDto>();
            MinimumPlayers = 4;
        }

        public string ToMessage() => $"Raid {BossName}, {Location}, {Time}";

        public Embed ToEmbed()
        {
            Color GetColor()
            {
                if (Users.Count >= MinimumPlayers)
                    return Color.Green;
                if (Users.Count >= MinimumPlayers / 2)
                    return Color.Orange;
                return Color.Red;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder
                .WithColor(GetColor())
                .AddInlineField("Boss", BossName)
                .AddInlineField("Kde", Location)
                .AddInlineField("Čas", Time.ToString(TimeFormat))
                //.AddInlineField("Počet lidí", MinimumPlayers)
                ;
            if (Users.Any())
            {
                string usersFieldValue = Users.Count >= 10 ?
                    UsersToGroupString(Users.Values) :
                    UsersToString(Users.Values);

                embedBuilder.AddField($"Lidi ({Users.Count})", usersFieldValue);
            }
            return embedBuilder.Build();
        }

        string UsersToString(IEnumerable<TeamUserDto> users) => string.Join(", ", users);

        string UsersToGroupString(IEnumerable<TeamUserDto> users)
        {
            List<string> formatterGroupedPlayers = new List<string>();

            string TeamToString(PokemonTeam? team) => team != null ? team.ToString() : "Bez teamu";

            var teams = new PokemonTeam?[] { PokemonTeam.Mystic, PokemonTeam.Instinct, PokemonTeam.Valor, null };
            foreach (PokemonTeam? team in teams)
            {
                var players = users.Where(t => t.Team == team).ToList();
                if (players.Any())
                    formatterGroupedPlayers.Add($"{TeamToString(team)} ({players.Count}): {UsersToString(players)}");
            }

            return string.Join(Environment.NewLine, formatterGroupedPlayers);
        }

        public static DateTime? ParseTime(string time)
        {
            var pieces = time.Split(' ', '.', ',', ':', ';');

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
                Created = message.CreatedAt.Date,
                BossName = embed.Fields[0].Value,
                Location = embed.Fields[1].Value,
                Time = time.Value,
            };

            return result;
        }
    }
}
