using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class RaidService
    {
        const ulong DefaultRaidChannelId = 348844165741936641;

        static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        readonly ILogger<RaidService> logger;
        readonly UserService userService;
        readonly RaidChannelService raidChannelService;

        public ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, RaidInfoDto>> Raids { get; } // <guildId, <messageId, RaidInfo>>

        public RaidService(ILogger<RaidService> logger, UserService userService, RaidChannelService raidChannelService)
        {
            Raids = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, RaidInfoDto>>();
            this.logger = logger;
            this.userService = userService;
            this.raidChannelService = raidChannelService;
        }

        public async Task OnNewGuild(SocketGuild guild)
        {
            if (!raidChannelService.IsKnown(guild.Id))
                // ignore unknown guilds for now
                return;

            Raids[guild.Id] = new ConcurrentDictionary<ulong, RaidInfoDto>();

            foreach (var channel in raidChannelService.GetRaidChannels(guild.Id))
                await UpdateRaidMessages(guild, channel);
        }

        public async Task UpdateRaidMessages(SocketGuild guild, IMessageChannel channel, int count = 10)
        {
            var batchMessages = await channel.GetMessagesAsync(count, options: retryOptions)
                .ToList();
            var latestMessages = batchMessages.SelectMany(t => t.Where(m => m.CreatedAt.UtcDateTime > DateTime.UtcNow.AddHours(-2)))
                .ToList();
            if (!latestMessages.Any())
                return;

            logger.LogInformation($"start updating raid messages for channel '{channel.Name}'");
            foreach (var message in latestMessages)
                if (message is IUserMessage userMessage)
                    await FixMessageAfterLoad(guild, userMessage);
            logger.LogInformation($"end updating raid messages for channel '{channel.Name}'");
        }

        async Task<bool> FixMessageAfterLoad(SocketGuild guild, IUserMessage message)
        {
            var raidInfo = RaidInfoDto.Parse(message);
            if (raidInfo == null || raidInfo.IsExpired)
                return false;

            logger.LogInformation($"Updating raid message '{message.Id}'");

            Raids[guild.Id][message.Id] = raidInfo;
            // Adjust user count
            var usersWithThumbsUp = await message.GetReactionUsersAsync(UnicodeEmojis.ThumbsUp);
            foreach (var user in usersWithThumbsUp.Where(t => !t.IsBot))
                raidInfo.Players[user.Id] = userService.GetPlayer(guild.GetUser(user.Id));

            // Extra players
            for (int i = 0; i < UnicodeEmojis.KeycapDigits.Length; i++)
            {
                var emoji = UnicodeEmojis.KeycapDigits[i];
                var usersWithKeycapReaction = await message.GetReactionUsersAsync(emoji);

                foreach (var user in usersWithKeycapReaction.Where(t => !t.IsBot))
                    raidInfo.ExtraPlayers.Add((user.Id, ExtraPlayerKeycapDigitToCount(emoji)));
            }

            await message.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());

            var allReactions = message.Reactions;
            var invalidReactions = allReactions.Where(t => !IsValidReactionEmote(t.Key.Name)).ToList();
            // Remove invalid reactions
            foreach (var react in invalidReactions)
            {
                var users = await message.GetReactionUsersAsync(react.Key.Name, options: retryOptions);
                foreach (var user in users)
                    await message.RemoveReactionAsync(react.Key, user, options: retryOptions);
            }

            return true;
        }

        public RaidInfoDto GetRaid(ulong guildId, int skip) =>
            Raids[guildId].Values
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .FirstOrDefault();

        public Task OnMessageDeleted(Cacheable<IMessage, ulong> cacheableMessage, ISocketMessageChannel channel)
        {
            if (!(channel is SocketTextChannel socketChannel))
                return Task.CompletedTask;

            var messageId = cacheableMessage.Id;
            if (Raids[socketChannel.Guild.Id].TryRemove(messageId, out _))
                logger.LogInformation($"Raid message '{messageId}' was removed.");

            return Task.CompletedTask;
        }

        public async Task SetDefaultReactions(IUserMessage message)
        {
            await message.AddReactionAsync(Emojis.ThumbsUp, retryOptions);
            await message.AddReactionAsync(Emojis.ThumbsDown, retryOptions);
        }

        bool IsValidReactionEmote(string emote) =>
            emote == UnicodeEmojis.ThumbsUp ||
            emote == UnicodeEmojis.ThumbsDown ||
            UnicodeEmojis.KeycapDigits.Contains(emote);

        int ExtraPlayerKeycapDigitToCount(string name) => Array.IndexOf(UnicodeEmojis.KeycapDigits, name) + 1;

        public async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!(channel is SocketGuildChannel socketGuildChannel))
                return;
            if (!Raids[socketGuildChannel.Guild.Id].TryGetValue(message.Id, out var raidInfo) || raidInfo.IsExpired)
                return;

            IUserMessage raidMessage = await message.GetOrDownloadAsync();
            if (reaction.Emote.Name == UnicodeEmojis.ThumbsUp)
            {
                if (raidInfo.Players.TryGetValue(reaction.UserId, out var player))
                {
                    logger.LogInformation($"Player '{player}' removed {nameof(UnicodeEmojis.ThumbsUp)} on raid {raidInfo.Message.Id}");
                    raidInfo.Players.Remove(reaction.UserId);
                    await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
                }
            }
            else if (Emojis.KeycapDigits.Contains(reaction.Emote))
            {
                var count = ExtraPlayerKeycapDigitToCount(reaction.Emote.Name);
                if (raidInfo.ExtraPlayers.Remove((reaction.UserId, count)))
                    await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
            }
        }

        public async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!(channel is SocketGuildChannel socketGuildChannel))
                return;
            if (!Raids[socketGuildChannel.Guild.Id].TryGetValue(message.Id, out var raidInfo) || raidInfo.IsExpired)
                return;

            IUserMessage raidMessage = await message.GetOrDownloadAsync();
            var user = socketGuildChannel.GetUser(reaction.UserId);

            if (!IsValidReactionEmote(reaction.Emote.Name))
            {
                await raidMessage.RemoveReactionAsync(reaction.Emote, user, retryOptions);
                return;
            }

            if (reaction.Emote.Name == UnicodeEmojis.ThumbsUp)
            {
                var player = userService.GetPlayer(user);
                raidInfo.Players[reaction.UserId] = player;
                logger.LogInformation($"Player '{player}' added {nameof(UnicodeEmojis.ThumbsUp)} on raid {raidInfo.Message.Id}");
                await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
            }
            else if (Emojis.KeycapDigits.Contains(reaction.Emote))
            {
                var count = ExtraPlayerKeycapDigitToCount(reaction.Emote.Name);
                raidInfo.ExtraPlayers.Add((reaction.UserId, count));
                await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
            }
        }
    }
}
