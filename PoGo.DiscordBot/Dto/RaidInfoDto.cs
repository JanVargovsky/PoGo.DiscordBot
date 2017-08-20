using Discord;
using Discord.WebSocket;
using PoGo.DiscordBot.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PoGo.DiscordBot.Dto
{
    public class IUserComparer : IEqualityComparer<IUser>
    {
        public int Compare(IUser x, IUser y)
        {
            return x.Id.CompareTo(y.Id);
        }

        public bool Equals(IUser x, IUser y)
        {
            return x.Id == y.Id;
        }

        public int GetHashCode(IUser obj)
        {
            return obj.GetHashCode();
        }
    }

    public class RaidInfoDto
    {
        public ulong CreatedByUserId { get; set; }
        public ulong MessageId { get; set; }
        public DateTime Created { get; set; }
        public string BossName { get; set; }
        public string Location { get; set; }
        public string Time { get; set; }
        public IDictionary<ulong, IUser> Users { get; set; }

        public RaidInfoDto()
        {
            Users = new Dictionary<ulong, IUser>();
        }

        public string ToMessage() => $"Raid {BossName}, {Location}, {Time}";

        public Embed ToEmbed()
        {
            string SocketGuildUserToString(SocketGuildUser user)
            {
                var team = UserModule.GetTeam(user);
                if (team.HasValue)
                    return $"{user.Nickname} ({PokemonTeamConverter.ToUnicode(team.Value)})";
                else
                    return $"{user.Nickname}";
            }
            string UserToString(IUser user)
            {
                if (user is SocketGuildUser socketGuildUser)
                    return SocketGuildUserToString(socketGuildUser);
                return user is IGuildUser guildUser ? guildUser.Nickname : user.Username;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.AddInlineField("Boss", BossName);
            embedBuilder.AddInlineField("Kde", Location);
            embedBuilder.AddInlineField("Čas", Time);
            if (Users.Any())
                embedBuilder.AddField($"Lidi ({Users.Count})", string.Join(", ", Users.Values.Select(UserToString)));
            return embedBuilder.Build();
        }

        public static RaidInfoDto Parse(IUserMessage message)
        {
            var embed = message.Embeds.FirstOrDefault();
            if (embed == null || embed.Fields.Length < 3)
                return null;

            var result = new RaidInfoDto
            {
                Created = message.CreatedAt.Date,
                BossName = embed.Fields[0].Value,
                Location = embed.Fields[1].Value,
                Time = embed.Fields[2].Value,
            };

            return result;
        }
    }
}
