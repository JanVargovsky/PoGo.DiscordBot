using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Properties;
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
    public class RaidModule : InteractiveBase<SocketCommandContext>
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
        [Summary("StartRaidSummary")]
        [RaidChannelPrecondition]
        public async Task StartRaid(
            [Summary("BossName")]string bossName,
            [Summary("Place")]string location,
            [Summary("Time (" + RaidInfoDto.TimeFormat + ").")]string time)
        {
            DateTime? parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync(Resources.BadTimeFormat + $"({RaidInfoDto.TimeFormat} 24H).");
                return;
            }

            if (parsedTime < DateTime.Now)
            {
                await ReplyAsync(Resources.PastRaid);
                return;
            }

            RaidChannelBindingDto raidChannelBinding = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id);

            RaidInfoDto raidInfo = new RaidInfoDto(RaidType.Normal)
            {
                BossName = bossName,
                Location = location,
                DateTime = parsedTime.Value,
            };

            System.Collections.Generic.IEnumerable<IRole> roles = teamService.GuildTeamRoles[Context.Guild.Id].TeamRoles.Values;
            bool shouldMention = !(configuration.GetGuildOptions(Context.Guild.Id)?.IgnoreMention ?? false);
            string mention = string.Empty;
            if (shouldMention)
                mention = raidChannelBinding.Mention == null ? string.Join(' ', roles.Select(t => t.Mention)) : raidChannelBinding.Mention.Mention;

            IUserMessage message = await raidChannelBinding.Channel.SendMessageAsync($"{raidInfo.ToSimpleString()} {mention}", embed: raidInfo.ToEmbed());
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
        [Summary("StartScheduledRaidSummary")]
        [RaidChannelPrecondition]
        public async Task StartScheduledRaid(
            [Summary("BossName")]string bossName,
            [Summary("Place")]string location,
            [Remainder][Summary("Date (" + RaidInfoDto.DateTimeFormat + ").")]string dateTime)
        {
            RaidChannelBindingDto raidChannelBinding = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id);
            if (raidChannelBinding == null || !raidChannelBinding.AllowScheduledRaids)
            {
                await ReplyAsync(Resources.RaidNotPossible);
                return;
            }

            DateTime? parsedDateTime = RaidInfoDto.ParseDateTime(dateTime);
            if (!parsedDateTime.HasValue)
            {
                await ReplyAsync((Resources.DateNotValid) + $"({RaidInfoDto.DateTimeFormat} 24H).");
                return;
            }

            if (parsedDateTime < DateTime.Now)
            {
                await ReplyAsync(Resources.PastRaid);
                return;
            }

            RaidInfoDto raidInfo = new RaidInfoDto(RaidType.Scheduled)
            {
                BossName = bossName,
                Location = location,
                DateTime = parsedDateTime.Value,
            };

            IUserMessage message = await raidChannelBinding.Channel.SendMessageAsync(string.Empty, embed: raidInfo.ToEmbed());
            logger.LogInformation($"New scheduled raid has been created '{bossName}' '{location}' '{parsedDateTime.Value.ToString(RaidInfoDto.DateTimeFormat)}'");
            raidInfo.Message = message;
            await Context.Message.AddReactionAsync(Emojis.Check);
            await raidService.SetDefaultReactions(message);

            raidStorageService.AddRaid(Context.Guild.Id, raidChannelBinding.Channel.Id, message.Id, raidInfo);
        }

        private RaidInfoDto GetRaid(int skip)
        {
            ulong raidChannelId = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id).Channel.Id;
            RaidInfoDto raid = raidStorageService.GetRaid(Context.Guild.Id, raidChannelId, skip);
            return raid;
        }

        [Command("time", RunMode = RunMode.Async)]
        [Alias("t")]
        [Summary("AdjustRaidTimeSummary")]
        [RaidChannelPrecondition]
        public async Task AdjustRaidTime(
            [Summary("Nový čas raidu (" + RaidInfoDto.TimeFormat + ").")]string time,
            [Summary("Počet anket odspodu.")] int skip = 0)
        {
            // TODO scheduled raid
            RaidInfoDto raid = GetRaid(skip);

            if (raid == null)
            {
                await ReplyAsync(Resources.RaidNotFound);
                return;
            }

            DateTime? parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync(Resources.BadTimeFormat + $" ({RaidInfoDto.TimeFormat} 24H).");
                return;
            }

            if (parsedTime < DateTime.Now)
            {
                await ReplyAsync(Resources.PastRaid);
                return;
            }

            SocketGuildUser currentUser = Context.User as SocketGuildUser;
            logger.LogInformation($"User '{currentUser.Nickname ?? Context.User.Username}' with id '{Context.User.Id}'" +
                $" changed raid with id '{raid.Message.Id}'" +
                $" time changed from {raid.DateTime.ToString(RaidInfoDto.TimeFormat)} to {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}");

            foreach (PlayerDto player in raid.Players.Values)
            {
                IGuildUser user = player.User;
                await user.SendMessageAsync(
                    $"Změna raid času z {raid.DateTime.ToString(RaidInfoDto.TimeFormat)} na {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}!" +
                    $" Jestli ti změna nevyhovuje, tak se odhlaš z raidu nebo se domluv s ostatními na jiném čase.");
            }

            raid.DateTime = parsedTime.Value;
            await raid.Message.ModifyAsync(t => t.Embed = raid.ToEmbed());
        }

        [Command("boss", RunMode = RunMode.Async)]
        [Alias("b")]
        [Summary("AdjustRaidBossSummary")]
        [RaidChannelPrecondition]
        public async Task AdjustRaidBoss(
            [Summary("Přenastaví bosse raidu.")]string boss,
            [Summary("Počet anket odspodu.")] int skip = 0)
        {
            RaidInfoDto raid = GetRaid(skip);

            if (raid == null)
            {
                await ReplyAsync(Resources.RaidNotFound);
                return;
            }

            SocketGuildUser currentUser = Context.User as SocketGuildUser;
            logger.LogInformation($"User '{currentUser.Nickname ?? Context.User.Username}' with id '{Context.User.Id}'" +
                $" changed raid with id '{raid.Message.Id}'" +
                $" boss changed from {raid.BossName} to {boss}");

            foreach (PlayerDto player in raid.Players.Values)
            {
                IGuildUser user = player.User;
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
            RaidInfoDto raid = GetRaid(skip);

            if (raid == null)
            {
                await ReplyAsync(Resources.RaidNotFound);
                return;
            }

            System.Collections.Generic.HashSet<PlayerDto> users = raid.Players.Values.ToHashSet();

            if (users.Count > 0)
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
        [Summary("DeleteRaidSummary")]
        [RaidChannelPrecondition]
        public async Task DeleteRaid(
            [Summary("Počet anket odspodu.")] int skip = 0)
        {
            RaidInfoDto raid = GetRaid(skip);

            if (raid == null)
            {
                await ReplyAsync("Raid nenalezen.");
                return;
            }

            IUserMessage questionMessage = await ReplyAsync($"Vážně chceš smazat tenhle raid: '{raid.ToSimpleString()}'? [y]");
            SocketMessage responseMessage = await NextMessageAsync();
            if (responseMessage == null || !string.Equals(responseMessage.Content, "y", StringComparison.OrdinalIgnoreCase))
                return;

            foreach (PlayerDto player in raid.Players.Values)
            {
                IGuildUser user = player.User;
                await user.SendMessageAsync($"Raid {raid.ToSimpleString()} se ruší!");
            }

            await raid.Message.DeleteAsync();
            await questionMessage.AddReactionAsync(Emojis.Check);
        }

        [Command("info", RunMode = RunMode.Async)]
        [Alias("i")]
        [Summary("RaidBossInfoSummary")]
        public async Task RaidBossInfo(
            [Summary("Název bosse.")] string bossName)
        {
            RaidBossDto boss = raidBossInfoService.GetBoss(bossName);

            if (boss == null)
            {
                string availableBosses = string.Join(", ", raidBossInfoService.GetAllKnownBossNames());
                await ReplyAsync($"Boss nenalezen - znám informace pouze o: {availableBosses}.");
                return;
            }

            string bossMention = raidBossInfoService.GetBossNameWithEmoji(boss.BossName, Context.Guild);
            System.Collections.Generic.IEnumerable<string> countersWithEmojis = boss.Counters?.Select(c => raidBossInfoService.GetBossNameWithEmoji(c, Context.Guild)) ?? Enumerable.Empty<string>();
            string countersField = string.Join(", ", countersWithEmojis);
            EmbedBuilder embedBuilder = new EmbedBuilder()
                .WithTitle(bossMention)
                .AddInlineField("Type", string.Join(", ", boss.Type))
                .AddInlineField("Weakness", string.Join(", ", boss.Weakness))
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
            [Remainder][Summary("Část názvu gymu")]string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                await ReplyAsync("Moc krátký název.");
                return;
            }

            System.Collections.Generic.IEnumerable<GymInfoDto> searchResult = gymLocationService.Search(Context.Guild.Id, name);
            if (searchResult == null)
            {
                await ReplyAsync("Server nepodporuje tenhle příkaz.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (GymInfoDto gymInfo in searchResult)
                sb.AppendLine($"{gymInfo.Name}: {gymLocationService.GetMapUrl(gymInfo)}");

            await ReplyAsync(sb.ToString());
        }

        [Command("list", RunMode = RunMode.Async)]
        [Summary("Vrátí seznam aktivních raidů včetně indexů.")]
        public async Task RaidList()
        {
            ulong channelId = raidChannelService.TryGetRaidChannelBinding(Context.Guild.Id, Context.Channel.Id).Channel.Id;
            System.Collections.Generic.IEnumerable<(int Index, RaidInfoDto Raid)> raids = raidStorageService.GetActiveRaidsWithIndexes(Context.Guild.Id, channelId);
            if (!raids.Any())
            {
                await ReplyAsync("Nejsou aktivní žádné raidy.");
                return;
            }
            string message = string.Join(Environment.NewLine, raids.Select(t => $"{t.Index} - {t.Raid.ToSimpleString()}").Reverse());
            await ReplyAsync($"```{message}```");
        }
    }
}