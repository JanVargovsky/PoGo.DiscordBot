using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class RaidService
    {
        private const ulong DefaultRaidChannelId = 348844165741936641;
        private static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        private readonly ILogger<RaidService> logger;
        private readonly UserService userService;
        private readonly RaidChannelService raidChannelService;
        private readonly RaidStorageService raidStorageService;

        public RaidService(ILogger<RaidService> logger, UserService userService, RaidChannelService raidChannelService, RaidStorageService raidStorageService)
        {
            this.logger = logger;
            this.userService = userService;
            this.raidChannelService = raidChannelService;
            this.raidStorageService = raidStorageService;
        }

        public async Task OnNewGuild(SocketGuild guild)
        {
            if (!raidChannelService.IsKnown(guild.Id))
                // ignore unknown guilds for now
                return;

            foreach (ITextChannel channel in raidChannelService.GetRaidChannels(guild.Id))
                await UpdateRaidMessages(guild, channel);
        }

        public async Task UpdateRaidMessages(SocketGuild guild, IMessageChannel channel, int count = 10)
        {
            try
            {
                RaidChannelBindingDto channelBinding = raidChannelService.TryGetRaidChannelBindingTo(guild.Id, channel.Id);
                bool mayContainScheduledRaids = channelBinding != null && channelBinding.AllowScheduledRaids;
                DateTime dateTimeFrom = !mayContainScheduledRaids ? DateTime.UtcNow.AddHours(-2) : DateTime.UtcNow.AddDays(-14);

                List<IReadOnlyCollection<IMessage>> batchMessages = await channel.GetMessagesAsync(count, options: retryOptions)
                    .ToList();
                List<IMessage> latestMessages = batchMessages.SelectMany(t => t.Where(m => m.CreatedAt.UtcDateTime > dateTimeFrom))
                    .ToList();
                if (!latestMessages.Any())
                    return;

                logger.LogInformation($"start updating raid messages for channel '{channel.Name}'");
                foreach (IMessage message in latestMessages)
                    if (message is IUserMessage userMessage)
                        await FixRaidMessageAfterLoad(guild, userMessage);
                logger.LogInformation($"end updating raid messages for channel '{channel.Name}'");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to update {guild.Name}/{channel.Name} ({ex.Message})");
            }
        }

        private async Task<bool> FixRaidMessageAfterLoad(SocketGuild guild, IUserMessage message)
        {
            RaidInfoDto raidInfo = RaidInfoDto.Parse(message);
            if (raidInfo == null)
                return false;

            logger.LogInformation($"Updating raid message '{message.Id}'");

            raidStorageService.AddRaid(guild.Id, message.Channel.Id, message.Id, raidInfo);
            // Adjust user count
            IReadOnlyCollection<IUser> allUsersWithThumbsUp = await message.GetReactionUsersAsync(UnicodeEmojis.ThumbsUp);
            IEnumerable<SocketGuildUser> usersWithThumbsUp = allUsersWithThumbsUp
                .Where(t => !t.IsBot)
                .Select(t => guild.GetUser(t.Id))
                .Where(t => t != null);
            foreach (SocketGuildUser user in usersWithThumbsUp)
                raidInfo.Players[user.Id] = userService.GetPlayer(guild.GetUser(user.Id));

            // Extra players
            for (int i = 0; i < UnicodeEmojis.KeycapDigits.Length; i++)
            {
                string emoji = UnicodeEmojis.KeycapDigits[i];
                IReadOnlyCollection<IUser> usersWithKeycapReaction = await message.GetReactionUsersAsync(emoji);

                foreach (IUser user in usersWithKeycapReaction.Where(t => !t.IsBot))
                    raidInfo.ExtraPlayers.Add((user.Id, ExtraPlayerKeycapDigitToCount(emoji)));
            }

            await message.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());

            IReadOnlyDictionary<IEmote, ReactionMetadata> allReactions = message.Reactions;
            List<KeyValuePair<IEmote, ReactionMetadata>> invalidReactions = allReactions.Where(t => !IsValidReactionEmote(t.Key.Name)).ToList();
            // Remove invalid reactions
            foreach (KeyValuePair<IEmote, ReactionMetadata> react in invalidReactions)
            {
                IReadOnlyCollection<IUser> users = await message.GetReactionUsersAsync(react.Key.Name, options: retryOptions);
                foreach (IUser user in users)
                    await message.RemoveReactionAsync(react.Key, user, options: retryOptions);
            }

            return true;
        }

        public Task OnMessageDeleted(Cacheable<IMessage, ulong> cacheableMessage, ISocketMessageChannel channel)
        {
            if (!(channel is SocketTextChannel socketChannel))
                return Task.CompletedTask;

            ulong messageId = cacheableMessage.Id;
            if (raidStorageService.TryRemove(socketChannel.Guild.Id, socketChannel.Id, messageId))
                logger.LogInformation($"Raid message '{messageId}' was removed.");

            return Task.CompletedTask;
        }

        public async Task SetDefaultReactions(IUserMessage message)
        {
            await message.AddReactionAsync(Emojis.ThumbsUp, retryOptions);
            await message.AddReactionAsync(Emojis.ThumbsDown, retryOptions);
        }

        private bool IsValidReactionEmote(string emote)
        {
            return emote == UnicodeEmojis.ThumbsUp ||
emote == UnicodeEmojis.ThumbsDown ||
UnicodeEmojis.KeycapDigits.Contains(emote);
        }

        private int ExtraPlayerKeycapDigitToCount(string name)
        {
            return Array.IndexOf(UnicodeEmojis.KeycapDigits, name) + 1;
        }

        public async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!(channel is SocketGuildChannel socketGuildChannel))
                return;
            RaidInfoDto raidInfo = raidStorageService.GetRaid(socketGuildChannel.Guild.Id, channel.Id, message.Id);
            if (raidInfo == null || raidInfo.IsExpired)
                return;

            IUserMessage raidMessage = await message.GetOrDownloadAsync();
            if (reaction.Emote.Name == UnicodeEmojis.ThumbsUp)
            {
                if (raidInfo.Players.TryGetValue(reaction.UserId, out PlayerDto player))
                {
                    logger.LogInformation($"Player '{player}' removed {nameof(UnicodeEmojis.ThumbsUp)} on raid {raidInfo.Message.Id}");
                    raidInfo.Players.Remove(reaction.UserId);
                    await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
                }
            }
            else if (Emojis.KeycapDigits.Contains(reaction.Emote))
            {
                int count = ExtraPlayerKeycapDigitToCount(reaction.Emote.Name);
                if (raidInfo.ExtraPlayers.Remove((reaction.UserId, count)))
                    await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
            }
        }

        public async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!(channel is SocketGuildChannel socketGuildChannel))
                return;
            RaidInfoDto raidInfo = raidStorageService.GetRaid(socketGuildChannel.Guild.Id, channel.Id, message.Id);
            if (raidInfo == null || raidInfo.IsExpired)
                return;

            IUserMessage raidMessage = await message.GetOrDownloadAsync();
            SocketGuildUser user = socketGuildChannel.GetUser(reaction.UserId);
            if (user.IsBot)
                return;
            if (!IsValidReactionEmote(reaction.Emote.Name))
            {
                await raidMessage.RemoveReactionAsync(reaction.Emote, user, retryOptions);
                return;
            }

            if (reaction.Emote.Name == UnicodeEmojis.ThumbsUp)
            {
                PlayerDto player = userService.GetPlayer(user);
                raidInfo.Players[reaction.UserId] = player;
                logger.LogInformation($"Player '{player}' added {nameof(UnicodeEmojis.ThumbsUp)} on raid {raidInfo.Message.Id}");
                await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
            }
            else if (Emojis.KeycapDigits.Contains(reaction.Emote))
            {
                int count = ExtraPlayerKeycapDigitToCount(reaction.Emote.Name);
                raidInfo.ExtraPlayers.Add((reaction.UserId, count));
                await raidMessage.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());
            }
        }

        public async Task UpdateRaidMessages()
        {
            List<(ulong guildId, ulong channelId, ulong messageId)> toRemove = new List<(ulong guildId, ulong channelId, ulong messageId)>();

            foreach ((ulong guildId, ulong channelId, ulong messageId, RaidInfoDto raidInfo) in raidStorageService.GetAll())
            {
                await raidInfo.Message.ModifyAsync(t => t.Embed = raidInfo.ToEmbed());

                if (raidInfo.IsExpired)
                    toRemove.Add((guildId, channelId, messageId));
            }

            foreach ((ulong guildId, ulong channelId, ulong messageId) in toRemove)
                raidStorageService.TryRemove(guildId, channelId, messageId);
        }
    }
}