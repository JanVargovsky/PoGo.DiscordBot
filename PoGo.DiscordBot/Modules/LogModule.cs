using Discord;
using Discord.Commands;
using PoGo.DiscordBot.Managers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class LogModule : ModuleBase
    {
        private readonly LogManager logManager;

        public LogModule(LogManager logManager)
        {
            this.logManager = logManager;
        }

        [Command("logs")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task GetLogs(int count = 10, int skip = 0)
        {
            var allLogs = logManager.GetLogs(count, skip).ToList();
            string messages = string.Join(Environment.NewLine, allLogs);
            await ReplyAsync(messages);
        }
    }
}
