using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireOwner]
    public class LogsModule : ModuleBase
    {
        private const string LogDirectory = "Logs";

        [Command("logs", RunMode = RunMode.Async)]
        public async Task GetLogsFiles()
        {
            DirectoryInfo di = new DirectoryInfo(LogDirectory);

            IEnumerable<string> filenames = di.EnumerateFiles().Select(t => t.Name);
            string content = string.Join(Environment.NewLine, filenames);

            EmbedBuilder embedBuilder = new EmbedBuilder()
                .WithDescription(content);
            await ReplyAsync(string.Empty, embed: embedBuilder.Build());
        }

        [Command("log", RunMode = RunMode.Async)]
        public async Task GetLog()
        {
            FileInfo fileInfo = new DirectoryInfo(LogDirectory)
                .EnumerateFiles()
                .OrderByDescending(t => t.LastWriteTimeUtc)
                .FirstOrDefault();

            if (fileInfo == null)
            {
                await ReplyAsync("No log has been created yet.");
                return;
            }

            await Context.Channel.SendFileAsync(fileInfo.FullName);
        }

        [Command("log", RunMode = RunMode.Async)]
        public async Task GetLog(string filename)
        {
            string path = Path.Combine(LogDirectory, filename);
            if (!File.Exists(path))
            {
                await ReplyAsync("Log does not exists.");
                return;
            }

            await Context.Channel.SendFileAsync(path);
        }
    }
}