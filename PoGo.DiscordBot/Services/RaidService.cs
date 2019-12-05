using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Services
{
    public class RaidService
    {
        const int ReactionUsersLimit = 100;

        static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        readonly ILogger<RaidService> logger;
        readonly UserService userService;
        readonly RaidChannelService raidChannelService;
        readonly RaidStorageService raidStorageService;
        readonly TimeService timeService;

        public RaidService(ILogger<RaidService> logger, UserService userService, RaidChannelService raidChannelService, RaidStorageService raidStorageService, TimeService timeService)
        {
            this.logger = logger;
            this.userService = userService;
            this.raidChannelService = raidChannelService;
            this.raidStorageService = raidStorageService;
            this.timeService = timeService;
        }

        public async Task OnNewGuild(SocketGuild guild)
        {
            if (!raidChannelService.IsKnown(guild.Id))
                // ignore unknown guilds for now
                return;

            foreach (var channel in raidChannelService.GetRaidChannels(guild.Id))
                await UpdateRaidMessages(guild, channel);
        }

        public async Task UpdateRaidMessages(SocketGuild guild, IMessageChannel channel, int count = 10)
        {
            try
            {
                var channelBinding = raidChannelService.TryGetRaidChannelBindingTo(guild.Id, channel.Id);
                var mayContainScheduledRaids = channelBinding != null && channelBinding.AllowScheduledRaids;
                var dateTimeFrom = !mayContainScheduledRaids ? DateTime.UtcNow.Date : DateTime.UtcNow.AddDays(-14);

                var batchMessages = await channel.GetMessagesAsync(count, options: retryOptions)
                    .ToList();
                var latestMessages = batchMessages.SelectMany(t => t.Where(m => m.CreatedAt.UtcDateTime > dateTimeFrom))
                    .ToList();
                if (!latestMessages.Any())
                    return;

                logger.LogInformation($"start updating raid messages for channel '{channel.Name}'");
                foreach (var message in latestMessages)
                    if (message is IUserMessage userMessage)
                        await FixRaidMessageAfterLoad(guild, userMessage);
                logger.LogInformation($"end updating raid messages for channel '{channel.Name}'");
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to update {guild.Name}/{channel.Name} ({ex.Message})");
            }
        }

        async Task<bool> FixRaidMessageAfterLoad(SocketGuild guild, IUserMessage message)
        {
            var raidInfo = ParseRaidInfo(message);
            if (raidInfo == null)
                return false;

            logger.LogInformation($"Updating raid message '{message.Id}'");

            raidStorageService.AddRaid(guild.Id, message.Channel.Id, message.Id, raidInfo);
            // Adjust user count
            var allUsersWithThumbsUp = await message.GetReactionUsersAsync(Emojis.ThumbsUp, ReactionUsersLimit).FlattenAsync();
            var usersWithThumbsUp = allUsersWithThumbsUp
                .Where(t => !t.IsBot)
                .Select(t => guild.GetUser(t.Id))
                .Where(t => t != null);
            foreach (var user in usersWithThumbsUp)
                raidInfo.Players[user.Id] = userService.GetPlayer(guild.GetUser(user.Id));

            // Extra players
            for (int i = 0; i < Emojis.KeycapDigits.Length; i++)
            {
                var emoji = Emojis.KeycapDigits[i];
                var usersWithKeycapReaction = await message.GetReactionUsersAsync(emoji, ReactionUsersLimit).FlattenAsync();

                foreach (var user in usersWithKeycapReaction.Where(t => !t.IsBot))
                    raidInfo.ExtraPlayers.Add((user.Id, ExtraPlayerKeycapDigitToCount(emoji.Name)));
            }

            await message.ModifyAsync(t =>
            {
                t.Content = string.Empty;
                t.Embed = ToEmbed(raidInfo);
            });

            var allReactions = message.Reactions;
            var invalidReactions = allReactions.Where(t => !IsValidReactionEmote(t.Key.Name)).ToList();
            // Remove invalid reactions
            foreach (var react in invalidReactions)
            {
                var users = await message.GetReactionUsersAsync(react.Key, ReactionUsersLimit, retryOptions).FlattenAsync();
                foreach (var user in users)
                    await message.RemoveReactionAsync(react.Key, user, retryOptions);
            }

            return true;
        }

        public Task OnMessageDeleted(Cacheable<IMessage, ulong> cacheableMessage, ISocketMessageChannel channel)
        {
            if (!(channel is SocketTextChannel socketChannel))
                return Task.CompletedTask;

            var messageId = cacheableMessage.Id;
            if (raidStorageService.TryRemove(socketChannel.Guild.Id, socketChannel.Id, messageId))
                logger.LogInformation($"Raid message '{messageId}' was removed.");

            return Task.CompletedTask;
        }

        public async Task SetDefaultReactions(IUserMessage message)
        {
            await message.AddReactionsAsync(new[] { Emojis.ThumbsUp, Emojis.KeycapDigits[0], Emojis.KeycapDigits[1] }, retryOptions);
        }

        bool IsValidReactionEmote(string emote) =>
            emote == UnicodeEmojis.ThumbsUp ||
            UnicodeEmojis.KeycapDigits.Contains(emote);

        int ExtraPlayerKeycapDigitToCount(string name) => Array.IndexOf(UnicodeEmojis.KeycapDigits, name);

        public async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!(channel is SocketGuildChannel socketGuildChannel))
                return;
            var raidInfo = raidStorageService.GetRaid(socketGuildChannel.Guild.Id, channel.Id, message.Id);
            if (raidInfo == null || raidInfo.IsExpired)
                return;

            IUserMessage raidMessage = await message.GetOrDownloadAsync();
            if (reaction.Emote.Equals(Emojis.ThumbsUp))
            {
                if (raidInfo.Players.TryGetValue(reaction.UserId, out var player))
                {
                    logger.LogInformation($"Player '{player}' removed {nameof(UnicodeEmojis.ThumbsUp)} on raid {raidInfo.Message.Id}");
                    raidInfo.Players.Remove(reaction.UserId);
                    await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
                }
            }
            else if (Emojis.KeycapDigits.Contains(reaction.Emote))
            {
                var count = ExtraPlayerKeycapDigitToCount(reaction.Emote.Name);
                if (raidInfo.ExtraPlayers.Remove((reaction.UserId, count)))
                    await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
            }
        }

        public async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!(channel is SocketGuildChannel socketGuildChannel))
                return;
            var raidInfo = raidStorageService.GetRaid(socketGuildChannel.Guild.Id, channel.Id, message.Id);
            if (raidInfo == null || raidInfo.IsExpired)
                return;

            IUserMessage raidMessage = await message.GetOrDownloadAsync();
            var user = socketGuildChannel.GetUser(reaction.UserId);
            if (user.IsBot)
                return;
            if (!IsValidReactionEmote(reaction.Emote.Name))
            {
                await raidMessage.RemoveReactionAsync(reaction.Emote, user, retryOptions);
                return;
            }

            if (reaction.Emote.Equals(Emojis.ThumbsUp))
            {
                var player = userService.GetPlayer(user);
                raidInfo.Players[reaction.UserId] = player;
                logger.LogInformation($"Player '{player}' added {nameof(UnicodeEmojis.ThumbsUp)} on raid {raidInfo.Message.Id}");
                await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
            }
            else if (Emojis.KeycapDigits.Contains(reaction.Emote))
            {
                var count = ExtraPlayerKeycapDigitToCount(reaction.Emote.Name);
                raidInfo.ExtraPlayers.Add((reaction.UserId, count));
                await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
            }
        }

        public async Task UpdateRaidMessages()
        {
            var toRemove = new List<(ulong guildId, ulong channelId, ulong messageId)>();

            foreach (var (guildId, channelId, messageId, raidInfo) in raidStorageService.GetAll())
            {
                await raidInfo.Message.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));

                if (raidInfo.IsExpired)
                    toRemove.Add((guildId, channelId, messageId));
            }

            foreach (var (guildId, channelId, messageId) in toRemove)
                raidStorageService.TryRemove(guildId, channelId, messageId);
        }

        RaidInfoDto ParseRaidInfo(IUserMessage message)
        {
            var embed = message.Embeds.FirstOrDefault();
            if (embed == null || embed.Fields.Length < 3)
                return null;

            RaidInfoDto result = null;

            if (embed.Fields[2].Name == "Čas")
            {
                var time = timeService.ParseTime(embed.Fields[2].Value, message.CreatedAt.Date);
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
            else if (embed.Fields[2].Name == "Datum")
            {
                var dateTime = timeService.ParseDateTime(embed.Fields[2].Value);
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

        public Embed ToEmbed(RaidInfoDto raidInfo)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder
                .WithColor(GetColor())
                .AddField("Boss", raidInfo.BossName, true)
                .AddField("Místo", raidInfo.Location, true)
                .AddField(raidInfo.RaidType == RaidType.Normal ? "Čas" : "Datum", RaidDateTimeToString(raidInfo.DateTime, raidInfo.RaidType), true)
                ;

            if (raidInfo.Players.Any())
            {
                string playerFieldValue = raidInfo.Players.Count >= 10 ?
                    PlayersToGroupString(raidInfo.Players.Values) :
                    PlayersToString(raidInfo.Players.Values);

                embedBuilder.AddField($"Hráči ({raidInfo.Players.Count})", playerFieldValue);
            }

            if (raidInfo.ExtraPlayers.Any())
            {
                string extraPlayersFieldValue = string.Join(" + ", raidInfo.ExtraPlayers.Select(t => t.Count));
                embedBuilder.AddField($"Další hráči (bez Discordu, 2. mobil atd.) ({raidInfo.ExtraPlayers.Sum(t => t.Count)})", extraPlayersFieldValue);
            }

            return embedBuilder.Build();

            Color GetColor()
            {
                if (raidInfo.RaidType == RaidType.Scheduled)
                {
                    return !raidInfo.IsExpired ? new Color(191, 155, 48) : Color.Red;
                }

                var remainingTime = raidInfo.DateTime - DateTime.UtcNow;

                if (remainingTime.TotalMinutes <= 0)
                    return Color.Red;
                if (remainingTime.TotalMinutes <= 15)
                    return Color.Orange;
                return Color.Green;
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
        }

        string RaidDateTimeToString(DateTime dt, RaidType rt) =>
            timeService.ConvertToLocalString(dt, rt == RaidType.Normal ? TimeService.TimeFormat : TimeService.DateTimeFormat);

        public string ToSimpleString(RaidInfoDto raidInfo) =>
            $"{raidInfo.BossName} {raidInfo.Location} {RaidDateTimeToString(raidInfo.DateTime, raidInfo.RaidType)}";
    }
}
