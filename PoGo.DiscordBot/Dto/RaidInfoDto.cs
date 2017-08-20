using Discord;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PoGo.DiscordBot.Dto
{
    public class RaidInfoDto
    {
        public ulong CreatedByUserId { get; set; }
        public ulong MessageId { get; set; }
        public DateTime Created { get; set; }
        public string BossName { get; set; }
        public string Location { get; set; }
        public string Time { get; set; }
        public ISet<IUser> Users { get; set; }

        public RaidInfoDto()
        {
            Users = new HashSet<IUser>();
        }

        public string ToMessage() => $"Raid {BossName}, {Location}, {Time}";

        public Embed ToEmbed()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.AddInlineField("Boss", BossName);
            embedBuilder.AddInlineField("Kde", Location);
            embedBuilder.AddInlineField("Čas", Time);
            if (Users.Any())
                embedBuilder.AddField($"Lidi ({Users.Count})", string.Join(", ", Users.Select(t => t.Username)));
            return embedBuilder.Build();
        }
    }
}
