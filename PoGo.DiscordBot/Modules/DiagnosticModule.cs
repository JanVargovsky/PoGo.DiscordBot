using Discord;
using Discord.Commands;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireOwner]
    public class DiagnosticModule : ModuleBase
    {
        [Command("ps")]
        public async Task ProcessInfo()
        {
            Process proc = Process.GetCurrentProcess();
            double mem = proc.WorkingSet64;
            TimeSpan cpu = proc.TotalProcessorTime;

            string[] suffixes = new[] { "", "K", "M", "G", "T" };
            int memoryIndex = 0;
            while (mem >= 1024)
            {
                mem /= 1024;
                memoryIndex++;
            }

            TimeSpan totalTime = DateTime.Now - proc.StartTime;

            EmbedBuilder embedBuilder = new EmbedBuilder()
                .AddField("Time running", $"{totalTime}")
                .AddField("Memory", $"{mem:n3} {suffixes[memoryIndex]}B")
                .AddField("CPU", $"{cpu.TotalSeconds:n3} sec")
                .AddField("Average CPU", $"{(cpu.TotalMilliseconds / totalTime.TotalMilliseconds):n3} %")
                ;

            await ReplyAsync(string.Empty, embed: embedBuilder.Build());
        }
    }
}