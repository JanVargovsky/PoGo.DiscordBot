﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Callbacks;
using PoGo.DiscordBot.Configuration;
using PoGo.DiscordBot.Dto;

namespace PoGo.DiscordBot.Services;

public class RaidService : IReactionAdded, IReactionRemoved, IGuildAvailable, IMessageDeleted, IConnected, IDisconnected, IAsyncDisposable
{
    private const int ReactionUsersLimit = 100;
    private const string Boss = "**Boss**";
    private const string Location = "**Místo**";
    private const string Time = "**Čas**";
    private const string Date = "**Datum**";
    private static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
    private readonly ILogger<RaidService> logger;
    private readonly UserService userService;
    private readonly RaidChannelService raidChannelService;
    private readonly RaidStorageService raidStorageService;
    private readonly TimeService timeService;
    private readonly Timer _updateRaidsTimer;

    public RaidService(ILogger<RaidService> logger, UserService userService, RaidChannelService raidChannelService, RaidStorageService raidStorageService, TimeService timeService)
    {
        this.logger = logger;
        this.userService = userService;
        this.raidChannelService = raidChannelService;
        this.raidStorageService = raidStorageService;
        this.timeService = timeService;

        _updateRaidsTimer = new Timer(async _ =>
        {
            await UpdateRaidMessages();
        }, null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task OnGuildAvailable(SocketGuild guild)
    {
        if (!raidChannelService.AddIfKnown(guild))
            // ignore unknown guilds for now
            return;

        logger.LogInformation($"Updating raid messages started for guild {guild.Name}");
        foreach (var channel in raidChannelService.GetRaidChannels(guild.Id))
            await UpdateRaidMessages(guild, channel);
        logger.LogInformation($"Updating raid messages ended for guild {guild.Name}");
    }

    public async Task UpdateRaidMessages(SocketGuild guild, IMessageChannel channel, int count = 10)
    {
        try
        {
            var channelBinding = raidChannelService.TryGetRaidChannelBindingTo(guild.Id, channel.Id);
            var mayContainScheduledRaids = channelBinding != null && channelBinding.AllowScheduledRaids;
            var dateTimeFrom = !mayContainScheduledRaids ? DateTime.UtcNow.Date : DateTime.UtcNow.AddDays(-14);

            var batchMessages = await channel.GetMessagesAsync(count, options: retryOptions)
                .ToListAsync();
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

    private async Task<bool> FixRaidMessageAfterLoad(SocketGuild guild, IUserMessage message)
    {
        var raidInfo = ParseRaidInfo(message);
        if (raidInfo == null || raidInfo.IsExpired)
            return false;

        logger.LogDebug($"Updating raid message '{message.Id}'");

        raidStorageService.AddRaid(guild.Id, message.Channel.Id, message.Id, raidInfo);

        // Adjust player count
        await AddPlayersAsync(raidInfo.Players, Emojis.ThumbsUp);

        // Adjust remote player count
        await AddPlayersAsync(raidInfo.RemotePlayers, Emojis.NoPedestrians);

        // Adjust invited player count
        await AddPlayersAsync(raidInfo.InvitedPlayers, Emojis.Handshake);

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

        async Task AddPlayersAsync(IDictionary<ulong, PlayerDto> storage, Emoji emoji)
        {
            var users = await message.GetReactionUsersAsync(emoji, ReactionUsersLimit).FlattenAsync();
            foreach (var user in users)
            {
                // ignore myself
                if (user.IsBot)
                    continue;

                var guildUser = guild.GetUser(user.Id);
                if (guildUser is null)
                {
                    // Bot may not have intent enabled
                    logger.LogWarning($"Did not find user '{user.Id}' on guild '{guild.Name}'");
                    continue;
                }

                storage[user.Id] = userService.GetPlayer(guildUser);
            }
        }
    }

    public async Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        if (await channel.GetOrDownloadAsync() is not SocketTextChannel socketChannel)
            return;

        var messageId = message.Id;
        if (raidStorageService.TryRemove(socketChannel.Guild.Id, channel.Id, messageId))
            logger.LogInformation($"Raid message '{messageId}' was removed.");
    }

    public async Task SetDefaultReactions(IUserMessage message)
    {
        await message.AddReactionsAsync(new[] {
                Emojis.ThumbsUp,
                Emojis.NoPedestrians,
                Emojis.Handshake,
                Emojis.KeycapDigits[0],
                Emojis.KeycapDigits[1],
                Emojis.KeycapDigits[2]
            }, retryOptions);
    }

    private bool IsValidReactionEmote(string emote) =>
        emote == UnicodeEmojis.ThumbsUp ||
        emote == UnicodeEmojis.NoPedestrians ||
        emote == UnicodeEmojis.Handshake ||
        UnicodeEmojis.KeycapDigits.Contains(emote);

    private int ExtraPlayerKeycapDigitToCount(string name) => Array.IndexOf(UnicodeEmojis.KeycapDigits, name) + 1;

    public async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (await channel.GetOrDownloadAsync() is not SocketGuildChannel socketGuildChannel)
            return;

        var raidInfo = raidStorageService.GetRaid(socketGuildChannel.Guild.Id, channel.Id, message.Id);
        if (raidInfo == null || raidInfo.IsExpired)
            return;

        IUserMessage raidMessage = await message.GetOrDownloadAsync();
        if (reaction.Emote.Equals(Emojis.ThumbsUp))
        {
            if (raidInfo.Players.Remove(reaction.UserId, out var player))
            {
                logger.LogInformation($"Player '{player}' removed {nameof(UnicodeEmojis.ThumbsUp)} on raid {raidInfo.Message.Id}");
                await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
            }
        }
        else if (reaction.Emote.Equals(Emojis.NoPedestrians))
        {
            if (raidInfo.RemotePlayers.Remove(reaction.UserId, out var player))
            {
                logger.LogInformation($"Remote player '{player}' removed {nameof(UnicodeEmojis.NoPedestrians)} on raid {raidInfo.Message.Id}");
                await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
            }
        }
        else if (reaction.Emote.Equals(Emojis.Handshake))
        {
            if (raidInfo.InvitedPlayers.Remove(reaction.UserId, out var player))
            {
                logger.LogInformation($"Invited player '{player}' removed {nameof(UnicodeEmojis.Handshake)} on raid {raidInfo.Message.Id}");
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

    public async Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (await channel.GetOrDownloadAsync() is not SocketGuildChannel socketGuildChannel)
            return;

        var raidInfo = raidStorageService.GetRaid(socketGuildChannel.Guild.Id, channel.Id, message.Id);
        if (raidInfo == null || raidInfo.IsExpired)
            return;

        var raidMessage = await message.GetOrDownloadAsync();
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
        else if (reaction.Emote.Equals(Emojis.NoPedestrians))
        {
            var player = userService.GetPlayer(user);
            raidInfo.RemotePlayers[reaction.UserId] = player;
            logger.LogInformation($"Remote player '{player}' added {nameof(UnicodeEmojis.NoPedestrians)} on raid {raidInfo.Message.Id}");
            await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
        }
        else if (reaction.Emote.Equals(Emojis.Handshake))
        {
            var player = userService.GetPlayer(user);
            raidInfo.InvitedPlayers[reaction.UserId] = player;
            logger.LogInformation($"Invited player '{player}' added {nameof(UnicodeEmojis.Handshake)} on raid {raidInfo.Message.Id}");
            await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
        }
        else if (Emojis.KeycapDigits.Contains(reaction.Emote))
        {
            var count = ExtraPlayerKeycapDigitToCount(reaction.Emote.Name);
            raidInfo.ExtraPlayers.Add((reaction.UserId, count));
            await raidMessage.ModifyAsync(t => t.Embed = ToEmbed(raidInfo));
        }
    }

    private async Task UpdateRaidMessages()
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

    private RaidInfoDto ParseRaidInfo(IUserMessage message)
    {
        var embed = message.Embeds.FirstOrDefault();
        if (embed == null)
            return null;

        if (TryParseEmbedWithDescription(out var result) ||
            TryParseEmbedWithFields(out result))
            return result;

        return null;

        // This is workaround format till Discord fix zero-width Embeds on Android
        bool TryParseEmbedWithDescription(out RaidInfoDto dto)
        {
            dto = null;
            if (string.IsNullOrEmpty(embed.Description) ||
                !embed.Description.StartsWith(Boss))
                return false;

            var rows = embed.Description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (rows.Length < 3 ||
                !rows[0].StartsWith(Boss) ||
                !rows[1].StartsWith(Location))
                return false;

            RaidType? raidType = null;
            if (rows[2].StartsWith(Time))
                raidType = RaidType.Normal;
            else if (rows[2].StartsWith(Date))
                raidType = RaidType.Scheduled;

            if (!raidType.HasValue)
                return false;

            const int SpaceLength = 1;
            var bossName = rows[0].Substring(Boss.Length + SpaceLength);
            var location = rows[1].Substring(Location.Length + SpaceLength);
            var dateTime = raidType switch
            {
                RaidType.Normal => timeService.ParseTime(rows[2].Substring(Time.Length + SpaceLength), message.CreatedAt.Date),
                RaidType.Scheduled => timeService.ParseDateTime(rows[2].Substring(Date.Length + SpaceLength)),
                _ => null,
            };

            if (!dateTime.HasValue)
                return false;

            dto = new RaidInfoDto(raidType.Value)
            {
                Message = message,
                CreatedAt = message.CreatedAt.UtcDateTime,
                BossName = bossName,
                Location = location,
                DateTime = dateTime.Value,
            };

            return true;
        }

        bool TryParseEmbedWithFields(out RaidInfoDto dto)
        {
            dto = null;
            if (embed.Fields.Length < 3)
            {
                return false;
            }

            if (embed.Fields[2].Name == "Čas")
            {
                var time = timeService.ParseTime(embed.Fields[2].Value, message.CreatedAt.Date);
                if (!time.HasValue)
                    return false;

                dto = new RaidInfoDto(RaidType.Normal)
                {
                    Message = message,
                    CreatedAt = message.CreatedAt.UtcDateTime,
                    BossName = embed.Fields[0].Value,
                    Location = embed.Fields[1].Value,
                    DateTime = time.Value,
                };
                return true;
            }
            else if (embed.Fields[2].Name == "Datum")
            {
                var dateTime = timeService.ParseDateTime(embed.Fields[2].Value);
                if (!dateTime.HasValue)
                    return false;

                result = new RaidInfoDto(RaidType.Scheduled)
                {
                    Message = message,
                    CreatedAt = message.CreatedAt.UtcDateTime,
                    BossName = embed.Fields[0].Value,
                    Location = embed.Fields[1].Value,
                    DateTime = dateTime.Value,
                };
                return true;
            }

            return false;
        }
    }

    public Embed ToEmbed(RaidInfoDto raidInfo)
    {
        var embedBuilder = new EmbedBuilder();
        embedBuilder
            .WithColor(GetColor())
            //.AddField("Boss", raidInfo.BossName, true)
            //.AddField("Místo", raidInfo.Location, true)
            //.AddField(raidInfo.RaidType == RaidType.Normal ? "Čas" : "Datum", RaidDateTimeToString(raidInfo.DateTime, raidInfo.RaidType), true)
            ;

        var description = new StringBuilder()
            .AppendLine(Boss + " " + raidInfo.BossName)
            .AppendLine(Location + " " + raidInfo.Location)
            .AppendLine((raidInfo.RaidType == RaidType.Normal ? Time : Date) + " " + RaidDateTimeToString(raidInfo.DateTime, raidInfo.RaidType));

        if (raidInfo.Players.Count > 0)
        {
            string playerFieldValue = raidInfo.Players.Count >= 10 ?
                PlayersToGroupString(raidInfo.Players.Values) :
                PlayersToString(raidInfo.Players.Values);

            //embedBuilder.AddField($"Hráči ({raidInfo.Players.Count})", playerFieldValue);
            description
                .AppendLine($"**Hráči ({raidInfo.Players.Count})**")
                .AppendLine(playerFieldValue);
        }

        if (raidInfo.RemotePlayers.Count > 0)
        {
            string remotePlayerFieldValue = PlayersToString(raidInfo.RemotePlayers.Values);
            //embedBuilder.AddField($"Vzdálení hráči ({raidInfo.RemotePlayers.Count})", remotePlayerFieldValue);
            description
                .AppendLine($"**Vzdálení hráči ({raidInfo.RemotePlayers.Count})**")
                .AppendLine(remotePlayerFieldValue);
        }

        if (raidInfo.InvitedPlayers.Count > 0)
        {
            string remotePlayerFieldValue = PlayersToString(raidInfo.InvitedPlayers.Values);
            description
                .AppendLine($"**Pozvaní hráči ({raidInfo.InvitedPlayers.Count})**")
                .AppendLine(remotePlayerFieldValue);
        }

        if (raidInfo.ExtraPlayers.Count > 0)
        {
            string extraPlayersFieldValue = string.Join(" + ", raidInfo.ExtraPlayers.Select(t => t.Count));
            //embedBuilder.AddField($"Další hráči (bez Discordu, 2. mobil atd.) ({raidInfo.ExtraPlayers.Sum(t => t.Count)})", extraPlayersFieldValue);
            description
                .AppendLine($"**Další hráči (bez Discordu, 2. mobil atd.) ({raidInfo.ExtraPlayers.Sum(t => t.Count)})**")
                .AppendLine(extraPlayersFieldValue);
        }

        // TODO: Do it configurable if Niantic changes the limit
        if (raidInfo.RemotePlayers.Count + raidInfo.InvitedPlayers.Count > 10)
            embedBuilder.WithTitle("❗❗❗ Všichni se do raidu nevlezou ❗❗❗");

        embedBuilder.WithDescription(description.ToString());

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

            var formatterGroupedPlayers = new List<string>();

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

    private string RaidDateTimeToString(DateTime dt, RaidType rt) =>
        timeService.ConvertToLocalString(dt, rt == RaidType.Normal ? TimeService.TimeFormat : TimeService.DateTimeFormat);

    public string ToSimpleString(RaidInfoDto raidInfo) =>
        $"{raidInfo.BossName} {raidInfo.Location} {RaidDateTimeToString(raidInfo.DateTime, raidInfo.RaidType)}";

    public Task OnConnected()
    {
        _updateRaidsTimer.Change(TimeSpan.FromSeconds(120 - DateTime.UtcNow.Second), TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    public Task OnDisconnected(Exception exception)
    {
        _updateRaidsTimer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_updateRaidsTimer != null)
            await _updateRaidsTimer.DisposeAsync();
    }
}
