using Discord;
using Discord.Commands;
using PoGo.DiscordBot.Dto;
using PoGo.DiscordBot.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class RaidModule : ModuleBase
    {
        static readonly RequestOptions retryOptions = new RequestOptions { RetryMode = RetryMode.AlwaysRetry, Timeout = 10000 };
        readonly RoleService roleService;
        readonly RaidService raidService;

        public RaidModule(RoleService roleService, RaidService raidService)
        {
            this.roleService = roleService;
            this.raidService = raidService;
        }

        [Command("restore", RunMode = RunMode.Async)]
        [Alias("r")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Restore()
        {
            var raidChannel = await raidService.GetRaidChannelAsync(Context.Guild);
            await raidService.UpdateRaidMessages(Context.Guild, raidChannel);
        }

        [Command("raid", RunMode = RunMode.Async)]
        public async Task StartRaid(string bossName, string location, string time, int minimumPlayers = 4)
        {
            var parsedTime = RaidInfoDto.ParseTime(time);
            if (!parsedTime.HasValue)
            {
                await ReplyAsync("Čas není ve validním formátu.");
                return;
            }

            var raidChannel = await raidService.GetRaidChannelAsync(Context.Guild);

            var raidInfo = new RaidInfoDto
            {
                Created = DateTime.UtcNow,
                CreatedByUserId = Context.User.Id,
                BossName = bossName,
                Location = location,
                Time = parsedTime.Value,
                MinimumPlayers = minimumPlayers,
            };

            var roles = await roleService.TeamRoles;
            var mention = string.Join(' ', roles.Values.Select(t => t.Mention));
            var message = await raidChannel.SendMessageAsync(mention, embed: raidInfo.ToEmbed());
            await raidService.SetDefaultReactions(message);
            raidInfo.MessageId = message.Id;
            raidService.Raids[raidInfo.MessageId] = raidInfo;
        }

        [Command("bind", RunMode = RunMode.Async)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task BindToChannel()
        {
            var channel = await Context.Guild.GetTextChannelAsync(Context.Channel.Id, options: retryOptions);
            raidService.SetRaidChannel(Context.Guild.Id, channel);
            await ReplyAsync("Raids are binded to this chanel.");
        }
    }
}
