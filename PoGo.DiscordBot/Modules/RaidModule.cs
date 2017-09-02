using Discord;
using Discord.Commands;
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

        public RaidModule(TeamService teamService, RaidService raidService)
        {
            this.teamService = teamService;
            this.raidService = raidService;
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
            await raidService.SetDefaultReactions(message);
            raidService.Raids[message.Id] = raidInfo;
        }

        [Command("time", RunMode = RunMode.Async)]
        public async Task AdjustLastRaidTime(string time)
        {
            var raid = raidService.Raids.Values
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();

            if(raid == null)
            {
                await ReplyAsync("Raid nenalezen");
                return;
            }

            var parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync($"Čas není ve validním formátu ({RaidInfoDto.TimeFormat}).");
                return;
            }

            raid.Time = parsedTime.Value;
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
