using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Services;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    [Group("raid")]
    [Alias("r")]
    public class RaidModule : ModuleBase<SocketCommandContext>
    {
        static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        readonly TeamService teamService;
        readonly RaidService raidService;
        readonly ILogger<RaidModule> logger;
        readonly RaidChannelService raidChannelService;
        readonly ConfigurationService configuration;
        readonly RaidBossInfoService raidBossInfoService;
        readonly GymLocationService gymLocationService;
        readonly RaidStorageService raidStorageService;

        public RaidModule(TeamService teamService, RaidService raidService, ILogger<RaidModule> logger, RaidChannelService raidChannelService,
            ConfigurationService configuration, RaidBossInfoService raidBossInfoService, GymLocationService gymLocationService, RaidStorageService raidStorageService)
        {
            this.teamService = teamService;
            this.raidService = raidService;
            this.logger = logger;
            this.raidChannelService = raidChannelService;
            this.configuration = configuration;
            this.raidBossInfoService = raidBossInfoService;
            this.gymLocationService = gymLocationService;
            this.raidStorageService = raidStorageService;
        }

        [Command("create", RunMode = RunMode.Async)]
        [Alias("c")]
        [Summary("Vytvoří raid anketu do speciálního kanálu.")]
        [RaidChannelPrecondition]
        public async Task StartRaid(
            [Summary("Název bosse.")]string bossName,
            [Summary("Místo.")]string location,
            [Summary("Čas (" + RaidInfoDto.TimeFormat + ").")]string time,
            [Summary("Doporučený minimální počet hráčů.")]int minimumPlayers = 4)
        {
            var parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync($"Čas není ve validním formátu ({RaidInfoDto.TimeFormat} 24H).");
                return;
            }

            if (parsedTime < DateTime.Now)
            {
                await ReplyAsync($"Vážně chceš vytvořit raid v minulosti?");
                return;
            }

            var raidChannelBinding = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id);

            var raidInfo = new RaidInfoDto(RaidType.Normal)
            {
                BossName = bossName,
                Location = location,
                DateTime = parsedTime.Value,
                MinimumPlayers = minimumPlayers,
            };

            var roles = teamService.GuildTeamRoles[Context.Guild.Id].TeamRoles.Values;
            bool shouldMention = !(configuration.GetGuildOptions(Context.Guild.Id)?.IgnoreMention ?? false);
            string mention = string.Empty;
            if (shouldMention)
                mention = raidChannelBinding.Mention == null ? string.Join(' ', roles.Select(t => t.Mention)) : raidChannelBinding.Mention.Mention;

            var message = await raidChannelBinding.Channel.SendMessageAsync($"{raidInfo.ToSimpleString()} {mention}", embed: raidInfo.ToEmbed());
            logger.LogInformation($"New raid has been created '{bossName}' '{location}' '{parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}'");
            raidInfo.Message = message;
            await Context.Message.AddReactionAsync(Emojis.Check);
            await raidService.SetDefaultReactions(message);

            raidStorageService.AddRaid(Context.Guild.Id, raidChannelBinding.Channel.Id, message.Id, raidInfo);

            await message.ModifyAsync(t =>
            {
                t.Content = string.Empty;
                t.Embed = raidInfo.ToEmbed(); // required workaround to set content to empty
            }, retryOptions);
        }

        [Command("schedule", RunMode = RunMode.Async)]
        [Alias("s")]
        [Summary("Vytvoří plánovanou raid anketu do speciálního kanálu.")]
        [RaidChannelPrecondition]
        public async Task StartScheduledRaid(
            [Summary("Název bosse.")]string bossName,
            [Summary("Místo.")]string location,
            [Remainder][Summary("Datum (" + RaidInfoDto.DateTimeFormat + ").")]string dateTime)
        {
            var raidChannelBinding = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id);
            if (raidChannelBinding == null || !raidChannelBinding.AllowScheduledRaids)
                return;

            var parsedDateTime = RaidInfoDto.ParseDateTime(dateTime);
            if (!parsedDateTime.HasValue)
            {
                await ReplyAsync($"Datum není ve validním formátu ({RaidInfoDto.DateTimeFormat} 24H).");
                return;
            }

            if (parsedDateTime < DateTime.Now)
            {
                await ReplyAsync($"Vážně chceš vytvořit plánovaný raid v minulosti?");
                return;
            }

            var raidInfo = new RaidInfoDto(RaidType.Scheduled)
            {
                BossName = bossName,
                Location = location,
                DateTime = parsedDateTime.Value,
                MinimumPlayers = null,
            };

            var message = await raidChannelBinding.Channel.SendMessageAsync(string.Empty, embed: raidInfo.ToEmbed());
            logger.LogInformation($"New scheduled raid has been created '{bossName}' '{location}' '{parsedDateTime.Value.ToString(RaidInfoDto.DateTimeFormat)}'");
            raidInfo.Message = message;
            await Context.Message.AddReactionAsync(Emojis.Check);
            await raidService.SetDefaultReactions(message);

            raidStorageService.AddRaid(Context.Guild.Id, raidChannelBinding.Channel.Id, message.Id, raidInfo);
        }

        RaidInfoDto GetRaid(int skip)
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
            [Summary("Nový čas raidu (" + RaidInfoDto.TimeFormat + ").")]string time,
            [Summary("Počet anket odspodu.")] int skip = 0)
        {
            // TODO scheduled raid
            var raid = GetRaid(skip);

            if (raid == null)
            {
                await ReplyAsync("Raid nenalezen.");
                return;
            }

            var parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync($"Čas není ve validním formátu ({RaidInfoDto.TimeFormat} 24H).");
                return;
            }

            if (parsedTime < DateTime.Now)
            {
                await ReplyAsync($"Vážně změnit čas do minulosti?");
                return;
            }

            var currentUser = Context.User as SocketGuildUser;
            logger.LogInformation($"User '{currentUser.Nickname ?? Context.User.Username}' with id '{Context.User.Id}'" +
                $" changed raid with id '{raid.Message.Id}'" +
                $" time changed from {raid.DateTime.ToString(RaidInfoDto.TimeFormat)} to {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}");

            foreach (var player in raid.Players.Values)
            {
                var user = player.User;
                await user.SendMessageAsync(
                    $"Změna raid času z {raid.DateTime.ToString(RaidInfoDto.TimeFormat)} na {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}!" +
                    $" Jestli ti změna nevyhovuje, tak se odhlaš z raidu nebo se domluv s ostatními na jiném čase.");
            }

            raid.DateTime = parsedTime.Value;
            await raid.Message.ModifyAsync(t => t.Embed = raid.ToEmbed());
        }

        [Command("boss", RunMode = RunMode.Async)]
        [Alias("b")]
        [Summary("Přenastaví bosse raidu.")]
        [RaidChannelPrecondition]
        public async Task AdjustRaidBoss(
            [Summary("Přenastaví bosse raidu.")]string boss,
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

            foreach (var player in raid.Players.Values)
            {
                var user = player.User;
                await user.SendMessageAsync($"Změna raid bosse z '{raid.BossName}' na '{boss}'!");
            }

            raid.BossName = boss;
            await raid.Message.ModifyAsync(t => t.Embed = raid.ToEmbed());
        }

        [Command("mention", RunMode = RunMode.Async)]
        [Alias("m")]
        [Summary("Označí lidi, kteří jsou zapsáni na raid.")]
        [RaidChannelPrecondition]
        public async Task MentionRaidPlayers(
            [Summary("Počet anket odspodu.")] int skip = 0,
            [Remainder][Summary("Text")]string text = null)
        {
            var raid = GetRaid(skip);

            if (raid == null)
            {
                await ReplyAsync("Raid nenalezen.");
                return;
            }

            var users = raid.Players.Values.ToHashSet();

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
            var raid = GetRaid(skip);

            if (raid == null)
            {
                await ReplyAsync("Raid nenalezen.");
                return;
            }

            foreach (var player in raid.Players.Values)
            {
                var user = player.User;
                await user.SendMessageAsync($"Raid {raid.ToSimpleString()} se ruší!");
            }

            await raid.Message.DeleteAsync();
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
                .AddInlineField($"Min. CP", boss.MinCP)
                .AddInlineField($"Max. CP", boss.MaxCP);
            if (!string.IsNullOrEmpty(countersField))
                embedBuilder.AddInlineField($"Protipokémoni", countersField);

            await ReplyAsync(string.Empty, embed: embedBuilder.Build());
        }

        [Command("location", RunMode = RunMode.Async)]
        [Alias("l", "loc")]
        [Summary("Vrátí lokaci gymu.")]
        public async Task RaidLocation(
            [Remainder][Summary("Část názvu gymu")]string name)
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

            await ReplyAsync(sb.ToString());
        }
    }
}
