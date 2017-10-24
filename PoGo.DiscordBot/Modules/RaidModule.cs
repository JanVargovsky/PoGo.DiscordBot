using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Modules.Preconditions;
using PoGo.DiscordBot.Services;
using System.Linq;
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

        public RaidModule(TeamService teamService, RaidService raidService, ILogger<RaidModule> logger, RaidChannelService raidChannelService)
        {
            this.teamService = teamService;
            this.raidService = raidService;
            this.logger = logger;
            this.raidChannelService = raidChannelService;
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
                await ReplyAsync($"Čas není ve validním formátu ({RaidInfoDto.TimeFormat}).");
                return;
            }

            var raidChannel = raidChannelService.TryGetRaidChannel(Context.Guild.Id, Context.Channel.Id);

            var raidInfo = new RaidInfoDto
            {
                BossName = bossName,
                Location = location,
                Time = parsedTime.Value,
                MinimumPlayers = minimumPlayers,
            };

            var roles = teamService.GuildTeamRoles[Context.Guild.Id].TeamRoles.Values;
            var mention = string.Join(' ', roles.Select(t => t.Mention));
            var message = await raidChannel.SendMessageAsync($"{raidInfo.ToSimpleString()} {mention}", embed: raidInfo.ToEmbed());
            logger.LogInformation($"New raid has been created '{bossName}' '{location}' '{parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}'");
            raidInfo.Message = message;
            await Context.Message.AddReactionAsync(Emojis.Check);
            await raidService.SetDefaultReactions(message);
            raidService.Raids[Context.Guild.Id][message.Id] = raidInfo;
            await message.ModifyAsync(t =>
            {
                t.Content = string.Empty;
                t.Embed = raidInfo.ToEmbed(); // required workaround to set content to empty
            }, retryOptions);
        }

        [Command("time", RunMode = RunMode.Async)]
        [Alias("t")]
        [Summary("Přenastaví čas raidu.")]
        [RaidChannelPrecondition]
        public async Task AdjustRaidTime(
            [Summary("Nový čas raidu (" + RaidInfoDto.TimeFormat + ").")]string time,
            [Summary("Počet anket odspodu.")] int skip = 0)
        {
            var raid = raidService.GetRaid(Context.Guild.Id, skip);

            if (raid == null)
            {
                await ReplyAsync("Raid nenalezen.");
                return;
            }

            var parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync($"Čas není ve validním formátu ({RaidInfoDto.TimeFormat}).");
                return;
            }

            var currentUser = Context.User as SocketGuildUser;
            logger.LogInformation($"User '{currentUser.Nickname ?? Context.User.Username}' with id '{Context.User.Id}'" +
                $" changed raid with id '{raid.Message.Id}'" +
                $" time changed from {raid.Time.ToString(RaidInfoDto.TimeFormat)} to {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}");

            foreach (var player in raid.Players.Values)
            {
                var user = player.User;
                await user.SendMessageAsync(
                    $"Změna raid času z {raid.Time.ToString(RaidInfoDto.TimeFormat)} na {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}!" +
                    $" Jestli ti změna nevyhovuje, tak se odhlaš z raidu nebo se domluv s ostatními na jiném čase.");
            }

            raid.Time = parsedTime.Value;
            await raid.Message.ModifyAsync(t => t.Embed = raid.ToEmbed());
        }

        [Command("boss", RunMode = RunMode.Async)]
        [Alias("b")]
        [Summary("Přenastaví bosse raidu.")]
        [RaidChannelPrecondition]
        public async Task AdjustBossTime(
            [Summary("Přenastaví bosse raidu.")]string boss,
            [Summary("Počet anket odspodu.")] int skip = 0)
        {
            var raid = raidService.GetRaid(Context.Guild.Id, skip);

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

        [Command("bind", RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task BindToChannel(string from, string to)
        {
            if (Context.Channel is ITextChannel channel)
            {
                raidChannelService.AddBinding(Context.Guild, from, to);
                await ReplyAsync("Binded");
            }
        }
    }
}
