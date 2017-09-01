using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class RaidService
    {
        static readonly string[] AllowedEmojis = new[] { Emojis.ThumbsUp, Emojis.ThumbsDown };

        const ulong DefaultRaidChannelId = 348844165741936641;

        static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        readonly StaticRaidChannels staticRaidChannels;
        readonly ILogger<RaidService> logger;
        private readonly UserService userService;

        public ConcurrentDictionary<ulong, RaidInfoDto> Raids { get; } // <messageId, RaidInfo>
        public ConcurrentDictionary<ulong, ITextChannel> RaidChannels { get; } // <guildId, RaidChannel>

        public RaidService(StaticRaidChannels staticRaidChannels, ILogger<RaidService> logger, UserService userService)
        {
            Raids = new ConcurrentDictionary<ulong, RaidInfoDto>();
            RaidChannels = new ConcurrentDictionary<ulong, ITextChannel>();
            this.staticRaidChannels = staticRaidChannels;
            this.logger = logger;
            this.userService = userService;
        }

        public async Task OnNewGuild(SocketGuild guild)
        {
            ITextChannel channel;
            if (staticRaidChannels.GuildToTextChannelBinding.TryGetValue(guild.Id, out var channelId))
            {
                channel = guild.GetTextChannel(channelId);
                logger.LogInformation($"Static guild {guild.Id} '{guild.Name}', channel {channelId} '{channel.Name}'");
            }
            else
            {
                channel = guild.DefaultChannel;
                logger.LogInformation($"New guild {guild.Id} '{guild.Name}', channel {channelId} '{channel.Name}'");
            }

            SetRaidChannel(guild.Id, channel);
            await UpdateRaidMessages(guild, channel);
        }

        public async Task UpdateRaidMessages(SocketGuild guild, IMessageChannel channel, int count = 10)
        {
            logger.LogInformation($"Updating raid messages");
            var batchMessages = channel.GetMessagesAsync(count, options: retryOptions).ToEnumerable();
            var now = DateTime.UtcNow.AddHours(-2);
            foreach (var messages in batchMessages)
                foreach (var message in messages)
                    if (message is IUserMessage userMessage && userMessage.Timestamp.UtcDateTime > now)
                        await FixMessageAfterLoad(guild, userMessage);
        }

        async Task FixMessageAfterLoad(SocketGuild guild, IUserMessage message)
        {
            var raidInfo = RaidInfoDto.Parse(message);
            if (raidInfo == null || raidInfo.IsExpired)
                return;

            Raids[message.Id] = raidInfo;
            // Adjust user count
            var usersWithThumbsUp = await message.GetReactionUsersAsync(Emojis.ThumbsUp);
            foreach (var user in usersWithThumbsUp.Where(t => !t.IsBot))
                raidInfo.Players[user.Id] = userService.GetPlayer(guild.GetUser(user.Id));

            logger.LogInformation($"Updating raid message '{message.Id}'");
            await message.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());

            var allReactions = message.Reactions;
            var invalidReactions = allReactions.Where(t => t.Key.Name != Emojis.ThumbsUp && t.Key.Name != Emojis.ThumbsDown);
            // Remove invalid reactions
            foreach (var react in invalidReactions)
            {
                var users = await message.GetReactionUsersAsync(react.Key.Name, options: retryOptions);
                foreach (var user in users)
                    await message.RemoveReactionAsync(react.Key, user, options: retryOptions);
            }
        }

        public async Task<ITextChannel> GetRaidChannelAsync(IGuild guild)
        {
            if (!RaidChannels.TryGetValue(guild.Id, out var channel))
            {
                channel = await guild.GetDefaultChannelAsync(options: retryOptions);
                SetRaidChannel(guild.Id, channel);
            }
            return channel;
        }

        public void SetRaidChannel(ulong id, ITextChannel channel)
        {
            RaidChannels[id] = channel;
        }

        public async Task SetDefaultReactions(IUserMessage message)
        {
            await message.AddReactionAsync(new Emoji(Emojis.ThumbsUp), retryOptions);
            await message.AddReactionAsync(new Emoji(Emojis.ThumbsDown), retryOptions);
        }

        bool IsValidReaction(SocketReaction reaction) => AllowedEmojis.Contains(reaction.Emote.Name);

        public async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!IsValidReaction(reaction) || !Raids.TryGetValue(message.Id, out var raidInfo) || raidInfo.IsExpired)
                return;

            if (reaction.Emote.Name == Emojis.ThumbsUp)
                if (raidInfo.Players.Remove(reaction.UserId))
                {
                    IUserMessage raidMessage = await message.GetOrDownloadAsync();
                    await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
                }
        }

        public async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!(channel is SocketGuildChannel socketGuildChannel))
                return;
            if (!IsValidReaction(reaction) || !Raids.TryGetValue(message.Id, out var raidInfo) || raidInfo.IsExpired)
                return;

            IUserMessage raidMessage = await message.GetOrDownloadAsync();
            var user = socketGuildChannel.GetUser(reaction.UserId);
            if (reaction.Emote.Name == Emojis.ThumbsUp)
            {
                raidInfo.Players[reaction.UserId] = userService.GetPlayer(user);
                await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
            }
            else if (reaction.Emote.Name != Emojis.ThumbsDown)
            {
                await raidMessage.RemoveReactionAsync(reaction.Emote, user, retryOptions);
            }
        }
    }
}
