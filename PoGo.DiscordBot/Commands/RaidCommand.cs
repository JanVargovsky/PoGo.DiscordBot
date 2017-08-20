using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Commands
{
    public class RaidCommand : ModuleBase
    {
        internal static ConcurrentDictionary<ulong, RaidInfoDto> ActiveRaids { get; } // <messageId, RaidInfo>
        static readonly RequestOptions retryOptions;

        static RaidCommand()
        {
            ActiveRaids = new ConcurrentDictionary<ulong, RaidInfoDto>();
            retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 5000 };
        }

        [Command("raid", RunMode = RunMode.Async)]
        public async Task StartRaid(string bossName, string location, string time)
        {
            var raidInfo = new RaidInfoDto
            {
                Created = DateTime.UtcNow,
                CreatedByUserId = Context.User.Id,
                BossName = bossName,
                Location = location,
                Time = time,
            };

            var message = await ReplyAsync(string.Empty, embed: raidInfo.ToEmbed());
            await SetDefaultReactions(message);
            raidInfo.MessageId = message.Id;
            while (!ActiveRaids.TryAdd(raidInfo.MessageId, raidInfo)) ;
        }

        async Task SetDefaultReactions(IUserMessage message)
        {
            await message.AddReactionAsync(new Emoji(Emojis.ThumbsUp), retryOptions);
            await message.AddReactionAsync(new Emoji(Emojis.ThumbsDown), retryOptions);
        }

        public static async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (ActiveRaids.TryGetValue(message.Id, out var raidInfo))
            {
                if (reaction.Emote.Name == Emojis.ThumbsUp)
                {
                    if (raidInfo.Users.Remove(reaction.User.Value))
                    {
                        IUserMessage raidMessage = await message.GetOrDownloadAsync();
                        await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
                    }
                }
            }
        }

        public static async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (ActiveRaids.TryGetValue(message.Id, out var raidInfo))
            {
                IUserMessage raidMessage = await message.GetOrDownloadAsync();
                if (reaction.Emote.Name == Emojis.ThumbsUp)
                {
                    raidInfo.Users.Add(reaction.User.Value);
                    await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
                }
                else if (reaction.Emote.Name != Emojis.ThumbsDown)
                {
                    var user = reaction.User.GetValueOrDefault();
                    await raidMessage.RemoveReactionAsync(reaction.Emote, user, retryOptions);
                }
            }
        }

        //[Command("raids", RunMode = RunMode.Async)]
        //public async Task ListActiveRaids()
        //{
        //    foreach (var raid in ActiveRaids.Values)
        //    {
        //        await ReplyAsync(raid.ToString());
        //    }
        //}
    }
}
