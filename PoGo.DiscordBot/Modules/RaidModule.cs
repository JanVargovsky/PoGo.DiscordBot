using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Services;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class RaidModule : ModuleBase
    {
        const ulong DefaultRaidChannelId = 348844165741936641;

        internal static ConcurrentDictionary<ulong, RaidInfoDto> Raids { get; } // <messageId, RaidInfo>
        static readonly RequestOptions retryOptions;
        static ITextChannel raidChannel;
        readonly RoleService roleService;

        static RaidModule()
        {
            Raids = new ConcurrentDictionary<ulong, RaidInfoDto>();
            retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        }

        public RaidModule(RoleService roleService)
        {
            this.roleService = roleService;
        }

        [Command("restore", RunMode = RunMode.Async)]
        [Alias("r")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Restore()
        {
            if (raidChannel == null)
                raidChannel = await GetRaidChannelAsync();

            await UpdateRaidMessages(raidChannel);
        }

        [Command("raid", RunMode = RunMode.Async)]
        public async Task StartRaid(string bossName, string location, string time, int minimumPlayers = 4)
        {
            if (raidChannel == null)
                raidChannel = await GetRaidChannelAsync();

            var raidInfo = new RaidInfoDto
            {
                Created = DateTime.UtcNow,
                CreatedByUserId = Context.User.Id,
                BossName = bossName,
                Location = location,
                Time = time,
                MinimumPlayers = minimumPlayers,
            };

            var roles = await roleService.TeamRoles;
            var mention = string.Join(' ', roles.Values.Select(t => t.Mention));
            var message = await raidChannel.SendMessageAsync(mention, embed: raidInfo.ToEmbed());
            await SetDefaultReactions(message);
            raidInfo.MessageId = message.Id;
            while (!Raids.TryAdd(raidInfo.MessageId, raidInfo)) ;
        }

        [Command("bind", RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task BindToChannel()
        {
            raidChannel = await GetRaidChannelAsync(Context.Message.Channel.Id);
            await ReplyAsync("Raids are binded to this chanel.");
        }

        Task<ITextChannel> GetRaidChannelAsync(ulong id = DefaultRaidChannelId) => Context.Guild.GetTextChannelAsync(id, options: retryOptions);

        async Task SetDefaultReactions(IUserMessage message)
        {
            await message.AddReactionAsync(new Emoji(Emojis.ThumbsUp), retryOptions);
            await message.AddReactionAsync(new Emoji(Emojis.ThumbsDown), retryOptions);
        }

        async Task UpdateRaidMessages(IMessageChannel channel, int count = 10)
        {
            var batchMessages = AsyncEnumerable.ToEnumerable(channel.GetMessagesAsync(count, options: retryOptions));
            var now = DateTime.Now.AddHours(-3);
            foreach (var messages in batchMessages)
            {
                foreach (var message in messages)
                {
                    if (message is IUserMessage userMessage && userMessage.Timestamp > now)
                        await FixMessageAfterLoad(userMessage);
                }
            }
        }

        async Task FixMessageAfterLoad(IUserMessage message)
        {
            var raidInfo = RaidInfoDto.Parse(message);
            if (raidInfo == null)
                return;

            while (!Raids.TryAdd(message.Id, raidInfo)) ;
            // Adjust user count
            var usersWithThumbsUp = await message.GetReactionUsersAsync(Emojis.ThumbsUp);
            foreach (var user in usersWithThumbsUp)
                if (!user.IsBot)
                    raidInfo.Users[user.Id] = await Context.Guild.GetUserAsync(user.Id);
            await message.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());

            var allReactions = message.Reactions;
            var invalidReactions = allReactions.Where(t => t.Key.Name != Emojis.ThumbsUp && t.Key.Name != Emojis.ThumbsDown);
            // Remove invalid reactions
            foreach (var react in invalidReactions)
            {
                var users = await message.GetReactionUsersAsync(react.Key.Name, options: retryOptions);
                foreach (var user in users)
                {
                    await message.RemoveReactionAsync(react.Key, user, options: retryOptions);
                }
            }
        }

        public static async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (Raids.TryGetValue(message.Id, out var raidInfo))
            {
                if (reaction.Emote.Name == Emojis.ThumbsUp)
                {
                    if (raidInfo.Users.Remove(reaction.UserId))
                    {
                        IUserMessage raidMessage = await message.GetOrDownloadAsync();
                        await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
                    }
                }
            }
        }

        public static async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (Raids.TryGetValue(message.Id, out var raidInfo))
            {
                IUserMessage raidMessage = await message.GetOrDownloadAsync();
                var user = await channel.GetUserAsync(reaction.UserId) as IGuildUser;
                if (reaction.Emote.Name == Emojis.ThumbsUp)
                {
                    raidInfo.Users.Add(reaction.UserId, user);
                    await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
                }
                else if (reaction.Emote.Name != Emojis.ThumbsDown)
                {
                    await raidMessage.RemoveReactionAsync(reaction.Emote, user, retryOptions);
                }
            }
        }
    }
}
