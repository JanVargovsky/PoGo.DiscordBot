using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Services;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    public class RaidModule : ModuleBase<SocketCommandContext>
    {
        static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        readonly TeamService teamService;
        readonly RaidService raidService;
        private readonly ILogger<RaidModule> logger;

        public RaidModule(TeamService teamService, RaidService raidService, ILogger<RaidModule> logger)
        {
            this.teamService = teamService;
            this.raidService = raidService;
            this.logger = logger;
        }

        [Command("raid", RunMode = RunMode.Async)]
        public async Task StartRaid(string bossName, string location, string time, int minimumPlayers = 4)
        {
            var parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync($"Čas není ve validním formátu ({RaidInfoDto.TimeFormat}).");
                return;
            }

            var raidChannel = raidService.GetRaidChannel(Context.Guild);

            var raidInfo = new RaidInfoDto
            {
                BossName = bossName,
                Location = location,
                Time = parsedTime.Value,
                MinimumPlayers = minimumPlayers,
            };

            var roles = teamService.GuildTeamRoles[Context.Guild.Id].TeamRoles.Values;
            var mention = string.Join(' ', roles.Select(t => t.Mention));
            var message = await raidChannel.SendMessageAsync(mention, embed: raidInfo.ToEmbed());
            raidInfo.Message = message;
            await Context.Message.AddReactionAsync(Emojis.Check);
            await raidService.SetDefaultReactions(message);
            raidService.Raids[message.Id] = raidInfo;
        }

        [Command("time", RunMode = RunMode.Async)]
        public async Task AdjustRaidTime(string time, int skip = 0)
        {
            var raid = raidService.Raids.Values
                .OrderByDescending(t => t.CreatedAt)
                .Skip(skip)
                .FirstOrDefault();

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

            logger.LogInformation($"Raid time change from {raid.Time.ToString(RaidInfoDto.TimeFormat)} to {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}");

            foreach (var player in raid.Players.Values)
            {
                var user = player.User;
                await user.SendMessageAsync($"Změna raid času z {raid.Time.ToString(RaidInfoDto.TimeFormat)} na {parsedTime.Value.ToString(RaidInfoDto.TimeFormat)}!");
            }

            raid.Time = parsedTime.Value;
            await raid.Message.ModifyAsync(t => t.Embed = raid.ToEmbed());
        }

        [Command("bind", RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task BindToChannel()
        {
            if (Context.Channel is ITextChannel channel)
            {
                raidService.SetRaidChannel(Context.Guild.Id, channel);
                await ReplyAsync("Raids are binded to this chanel.");
            }
        }
    }
}
