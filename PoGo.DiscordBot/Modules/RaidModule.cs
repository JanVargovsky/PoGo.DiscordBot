﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Services;

namespace PoGo.DiscordBot.Modules;

[RequireContext(ContextType.Guild)]
[Group("raid")]
[Alias("r")]
public class RaidModule : ModuleBase<SocketCommandContext>
{
    private static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
    private readonly TeamService teamService;
    private readonly RaidService raidService;
    private readonly ILogger<RaidModule> logger;
    private readonly RaidChannelService raidChannelService;
    private readonly ConfigurationService configuration;
    private readonly RaidBossInfoService raidBossInfoService;
    private readonly GymLocationService gymLocationService;
    private readonly RaidStorageService raidStorageService;
    private readonly TimeService timeService;

    public RaidModule(TeamService teamService, RaidService raidService, ILogger<RaidModule> logger, RaidChannelService raidChannelService,
        ConfigurationService configuration, RaidBossInfoService raidBossInfoService, GymLocationService gymLocationService, RaidStorageService raidStorageService,
        TimeService timeService)
    {
        this.teamService = teamService;
        this.raidService = raidService;
        this.logger = logger;
        this.raidChannelService = raidChannelService;
        this.configuration = configuration;
        this.raidBossInfoService = raidBossInfoService;
        this.gymLocationService = gymLocationService;
        this.raidStorageService = raidStorageService;
        this.timeService = timeService;
    }

    [Command("create", RunMode = RunMode.Async)]
    [Alias("c")]
    [Summary("Vytvoří raid anketu do speciálního kanálu.")]
    [RaidChannelPrecondition]
    public async Task RaidCreate(
        [Summary("Název bosse.")] string bossName,
        [Summary("Místo.")] string location,
        [Summary("Čas (" + TimeService.TimeFormat + ").")] DateTime time)
    {
        time = timeService.EnsureUtc(time);
        if (time < DateTime.UtcNow)
        {
            await ReplyAsync($"Vážně chceš vytvořit raid v minulosti?");
            return;
        }
        if (!timeService.IsToday(time))
        {
            await ReplyAsync($"Raid není dnes.");
            return;
        }

        var raidChannelBinding = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id);

        var raidInfo = new RaidInfoDto(RaidType.Normal)
        {
            BossName = bossName,
            Location = location,
            DateTime = time,
        };

        var roles = teamService.GuildTeamRoles[Context.Guild.Id].TeamRoles.Values;
        bool shouldMention = !(configuration.GetGuildOptions(Context.Guild.Id)?.IgnoreMention ?? false);
        string mention = string.Empty;
        if (shouldMention)
            mention = raidChannelBinding.Mention == null ? string.Join(' ', roles.Select(t => t.Mention)) : raidChannelBinding.Mention.Mention;

        var message = await raidChannelBinding.Channel.SendMessageAsync($"{raidService.ToSimpleString(raidInfo)} {mention}", embed: raidService.ToEmbed(raidInfo));
        logger.LogInformation($"New raid has been created '{raidService.ToSimpleString(raidInfo)}'");
        raidInfo.Message = message;
        await Context.Message.AddReactionAsync(Emojis.Check);
        await raidService.SetDefaultReactions(message);

        raidStorageService.AddRaid(Context.Guild.Id, raidChannelBinding.Channel.Id, message.Id, raidInfo);

        await message.ModifyAsync(t =>
        {
            t.Content = string.Empty;
            t.Embed = raidService.ToEmbed(raidInfo);
        }, retryOptions);
    }

    [Command("create", RunMode = RunMode.Async)]
    [Alias("c")]
    [Summary("Vytvoří raid anketu do speciálního kanálu.")]
    [RaidChannelPrecondition]
    public Task RaidCreate(
        [Summary("Název bosse.")] string bossName,
        [Summary("Místo.")] string location,
        [Summary("Počet minut za jak dlouho má být anketa.")] int minutes)
    {
        var time = DateTime.UtcNow.AddMinutes(minutes);
        return RaidCreate(bossName, location, time);
    }

    [Command("schedule", RunMode = RunMode.Async)]
    [Alias("s")]
    [Summary("Vytvoří plánovanou raid anketu do speciálního kanálu.")]
    [RaidChannelPrecondition]
    public async Task StartScheduledRaid(
        [Summary("Název bosse.")] string bossName,
        [Summary("Místo.")] string location,
        [Remainder][Summary("Datum (" + TimeService.DateTimeFormat + ").")] string dateTime)
    {
        var raidChannelBinding = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id);
        if (raidChannelBinding == null || !raidChannelBinding.AllowScheduledRaids)
        {
            await ReplyAsync($"Z tohoto kanálu není možné vytvořit plánovanou raid anketu.");
            return;
        }

        var parsedDateTime = timeService.ParseDateTime(dateTime);
        if (!parsedDateTime.HasValue)
        {
            await ReplyAsync($"Datum není ve validním formátu ({TimeService.DateTimeFormat} 24H).");
            return;
        }

        if (parsedDateTime < DateTime.UtcNow)
        {
            await ReplyAsync($"Vážně chceš vytvořit plánovaný raid v minulosti?");
            return;
        }

        var raidInfo = new RaidInfoDto(RaidType.Scheduled)
        {
            BossName = bossName,
            Location = location,
            DateTime = parsedDateTime.Value,
        };

        var message = await raidChannelBinding.Channel.SendMessageAsync(string.Empty, embed: raidService.ToEmbed(raidInfo));
        logger.LogInformation($"New scheduled raid has been created '{raidService.ToSimpleString(raidInfo)}'");
        raidInfo.Message = message;
        await Context.Message.AddReactionAsync(Emojis.Check);
        await raidService.SetDefaultReactions(message);

        raidStorageService.AddRaid(Context.Guild.Id, raidChannelBinding.Channel.Id, message.Id, raidInfo);
    }

    private RaidInfoDto GetRaid(int skip)
    {
        var raidChannelId = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id).Channel.Id;
        var raid = raidStorageService.GetRaid(Context.Guild.Id, raidChannelId, skip);
        return raid;
    }

    [Command("time", RunMode = RunMode.Async)]
    [Alias("t")]
    [Summary("Přenastaví čas raidu.")]
    [RaidChannelPrecondition]
    public async Task AdjustRaidTime(
        [Summary("Nový čas raidu (" + TimeService.TimeFormat + ").")] string time,
        [Summary("Počet anket odspodu.")] int skip = 0)
    {
        // TODO scheduled raid
        var raid = GetRaid(skip);

        if (raid == null)
        {
            await ReplyAsync("Raid nenalezen.");
            return;
        }

        var parsedTime = timeService.ParseTime(time);
        if (!parsedTime.HasValue)
        {
            await ReplyAsync($"Čas není ve validním formátu ({TimeService.TimeFormat} 24H).");
            return;
        }

        if (parsedTime < DateTime.UtcNow)
        {
            await ReplyAsync($"Vážně změnit čas do minulosti?");
            return;
        }

        var currentUser = Context.User as SocketGuildUser;
        logger.LogInformation($"User '{currentUser.Nickname ?? Context.User.Username}' with id '{Context.User.Id}'" +
            $" changed raid with id '{raid.Message.Id}'" +
            $" time changed from {timeService.ConvertToLocalString(raid.DateTime, TimeService.TimeFormat)} to {timeService.ConvertToLocalString(parsedTime.Value, TimeService.TimeFormat)}");

        foreach (var player in raid.GetAllPlayers())
        {
            var user = player.User;
            await user.SendMessageAsync(
                $"Změna raid času z {timeService.ConvertToLocalString(raid.DateTime, TimeService.TimeFormat)} na {timeService.ConvertToLocalString(parsedTime.Value, TimeService.TimeFormat)}!" +
                $" Jestli ti změna nevyhovuje, tak se odhlaš z raidu nebo se domluv s ostatními na jiném čase.");
        }

        raid.DateTime = parsedTime.Value;
        await raid.Message.ModifyAsync(t => t.Embed = raidService.ToEmbed(raid));
    }

    [Command("boss", RunMode = RunMode.Async)]
    [Alias("b")]
    [Summary("Přenastaví bosse raidu.")]
    [RaidChannelPrecondition]
    public async Task AdjustRaidBoss(
        [Summary("Přenastaví bosse raidu.")] string boss,
        [Summary("Počet anket odspodu.")] int skip = 0)
    {
        var raid = GetRaid(skip);

        if (raid == null)
        {
            await ReplyAsync("Raid nenalezen.");
            return;
        }

        var currentUser = Context.User as SocketGuildUser;
        logger.LogInformation($"User '{currentUser.Nickname ?? Context.User.Username}' with id '{Context.User.Id}'" +
            $" changed raid with id '{raid.Message.Id}'" +
            $" boss changed from {raid.BossName} to {boss}");

        foreach (var player in raid.GetAllPlayers())
        {
            var user = player.User;
            await user.SendMessageAsync($"Změna raid bosse z '{raid.BossName}' na '{boss}'!");
        }

        raid.BossName = boss;
        await raid.Message.ModifyAsync(t => t.Embed = raidService.ToEmbed(raid));
    }

    [Command("mention", RunMode = RunMode.Async)]
    [Alias("m")]
    [Summary("Označí lidi, kteří jsou zapsáni na raid.")]
    [RaidChannelPrecondition]
    public async Task MentionRaidPlayers(
        [Summary("Počet anket odspodu.")] int skip = 0,
        [Remainder][Summary("Text")] string text = null)
    {
        var raid = GetRaid(skip);

        if (raid == null)
        {
            await ReplyAsync("Raid nenalezen.");
            return;
        }

        var users = raid.GetAllPlayers();

        if (users.Any())
        {
            string playerMentions = string.Join(' ', users.Select(t => t.User.Mention));
            string message = string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
                message = text + Environment.NewLine;
            message += playerMentions;
            await ReplyAsync(message);
        }
    }

    [Command("delete", RunMode = RunMode.Async)]
    [Alias("d")]
    [Summary("Smaže raid.")]
    [RaidChannelPrecondition]
    public async Task DeleteRaid(
        [Summary("Počet anket odspodu.")] int skip = 0)
    {
        // TODO:
        await ReplyAsync("Tenhle prikaz je docasne nepristupny.");
        return;

        //var raid = GetRaid(skip);

        //if (raid == null)
        //{
        //    await ReplyAsync("Raid nenalezen.");
        //    return;
        //}

        //var questionMessage = await ReplyAsync($"Vážně chceš smazat tenhle raid: '{raidService.ToSimpleString(raid)}'? [y]");
        //var responseMessage = await NextMessageAsync();
        //if (responseMessage == null || responseMessage.Content.ToLower() != "y")
        //    return;

        //foreach (var player in raid.GetAllPlayers())
        //{
        //    var user = player.User;
        //    await user.SendMessageAsync($"Raid {raidService.ToSimpleString(raid)} se ruší!");
        //}

        //await raid.Message.DeleteAsync();
        //await questionMessage.AddReactionAsync(Emojis.Check);
    }

    [Command("info", RunMode = RunMode.Async)]
    [Alias("i")]
    [Summary("Vypíše základní info o bossovi.")]
    public async Task RaidBossInfo(
        [Summary("Název bosse.")] string bossName)
    {
        var boss = raidBossInfoService.GetBoss(bossName);

        if (boss == null)
        {
            var availableBosses = string.Join(", ", raidBossInfoService.GetAllKnownBossNames());
            await ReplyAsync($"Boss nenalezen - znám informace pouze o: {availableBosses}.");
            return;
        }

        string bossMention = raidBossInfoService.GetBossNameWithEmoji(boss.BossName, Context.Guild);
        var countersWithEmojis = boss.Counters?.Select(c => raidBossInfoService.GetBossNameWithEmoji(c, Context.Guild)) ?? Enumerable.Empty<string>();
        var countersField = string.Join(", ", countersWithEmojis);
        EmbedBuilder embedBuilder = new EmbedBuilder()
            .WithTitle(bossMention)
            .AddField("Type", string.Join(", ", boss.Type), true)
            .AddField("Weakness", string.Join(", ", boss.Weakness), true)
            .AddField("CP", string.Join(Environment.NewLine, boss.CPs.Select(t => $"{t.Key}: {t.Value}")))
            .AddField($"Charge attack{(boss.ChargeAttacks.Length > 1 ? "s" : string.Empty)}", string.Join(Environment.NewLine, boss.ChargeAttacks));
        if (!string.IsNullOrEmpty(countersField))
            embedBuilder.AddField($"Counters", countersField);

        await ReplyAsync(string.Empty, embed: embedBuilder.Build());
    }

    [Command("location", RunMode = RunMode.Async)]
    [Alias("l", "loc")]
    [Summary("Vrátí lokaci gymu.")]
    public async Task RaidLocation(
        [Remainder][Summary("Část názvu gymu")] string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 3)
        {
            await ReplyAsync("Moc krátký název.");
            return;
        }

        var searchResult = gymLocationService.Search(Context.Guild.Id, name);
        if (searchResult == null)
        {
            await ReplyAsync("Server nepodporuje tenhle příkaz.");
            return;
        }

        var sb = new StringBuilder();
        foreach (var gymInfo in searchResult)
            sb.AppendLine($"{gymInfo.Name}: {gymLocationService.GetMapUrl(gymInfo)}");

        await ReplyAsync(sb.Length > 0 ? sb.ToString() : "Nic nenalezeno.");
    }

    [Command("list", RunMode = RunMode.Async)]
    [Summary("Vrátí seznam aktivních raidů včetně indexů.")]
    public async Task RaidList()
    {
        var channelId = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id).Channel.Id;
        var raids = raidStorageService.GetActiveRaidsWithIndexes(Context.Guild.Id, channelId);
        if (!raids.Any())
        {
            await ReplyAsync("Nejsou aktivní žádné raidy.");
            return;
        }
        string message = string.Join(Environment.NewLine, raids.Select(t => $"{t.Index} - {raidService.ToSimpleString(t.Raid)}").Reverse());
        await ReplyAsync($"```{message}```");
    }
}
