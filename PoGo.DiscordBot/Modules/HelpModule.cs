using Discord;
using Discord.Commands;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        readonly CommandService commandService;
        readonly IServiceProvider serviceProvider;
        readonly char prefix;

        public HelpModule(CommandService commandService, IServiceProvider serviceProvider, IOptions<ConfigurationOptions> config)
        {
            this.commandService = commandService;
            this.serviceProvider = serviceProvider;
            prefix = config.Value.Prefix;
        }

        [Command("help")]
        public async Task Help()
        {
            var builder = new EmbedBuilder()
                .WithColor(Color.Blue);

            HashSet<string> commands = new HashSet<string>();

            foreach (var module in commandService.Modules)
                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context, serviceProvider);
                    if (result.IsSuccess)
                        commands.Add($"{prefix}{cmd.Aliases.First()}");
                }

            string orderedCommandsString = string.Join(Environment.NewLine, commands.OrderBy(t => t));
            builder.AddField("Dostupné příkazy", orderedCommandsString);

            builder.AddField("Nápověda ke konkrétnímu příkazu", "Pro detailnější nápovědu k příkazu napiš **!help <příkaz>** kde příkaz je jeden z výše uvedených příkazů." +
                $@" Jestliže je příkaz složen z více slov (třeba příkaz {prefix}stats team) je nutné ho obalit uvozovkami **{prefix}help ""stats team""**.");

            builder.AddField("Použití příkazu",
                $"BOT reaguje na všechny zprávy, které začínají nějakým znakem." +
                $" V našem případě je to znak **{prefix}**." +
                $" Pokud tedy přijde zpráva např. **!raid**, tak je předána k zpracování." +
                $" Každý příkaz má přesně dané parametry - **ty je nutné dodržovat, jinak se příkaz vůbec nevykoná**." +
                $" Jestliže má tedy příkaz **raid** 3 parametry - bossName, location a time, je nutné je všechny předat." +
                $" Napíšu tedy tohle: **!raid Tyranitar Stoun 15:30**");

            await ReplyAsync(string.Empty, embed: builder.Build());
        }

        private enum CommandInfoSignature
        {
            Basic,
            Full
        }

        [Command("help")]
        public async Task Help([Remainder] string command)
        {
            var result = commandService.Search(Context, command);

            if (!result.IsSuccess)
            {
                await ReplyAsync($"Žádný příkaz **{command}** jsem nemohl najít.");
                return;
            }

            var builder = new EmbedBuilder()
                .WithColor(Color.Blue);

            string ParameterInfoToString(ParameterInfo info) => !info.IsOptional ? info.Name : $"[{info.Name}]";

            string ParameterInfoToDetailedString(ParameterInfo info)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(info.Name);

                if (!string.IsNullOrEmpty(info.Summary))
                    sb.Append($" - {info.Summary}");

                if (info.Type.IsEnum)
                    sb.Append($" Možné jsou jenom tyhle hodnoty ({string.Join(" | ", Enum.GetNames(info.Type))})!");

                if (info.IsOptional)
                    sb.Append($" (Volitelný, výchozí hodnota je {info.DefaultValue})");

                return sb.ToString();
            }

            string CommandInfoSignature(CommandInfo ci, CommandInfoSignature signature)
            {
                StringBuilder sb = new StringBuilder()
                    .Append(prefix)
                    .Append(ci.Name);

                string FormatParameter(ParameterInfo pi) => $"<{pi.Name}>";

                if (ci.Parameters.Any())
                {
                    var parameters = ci.Parameters.AsEnumerable();

                    if (signature == HelpModule.CommandInfoSignature.Basic)
                        parameters = parameters.Where(pi => !pi.IsOptional);

                    sb.Append(' ')
                        .AppendJoin(' ', parameters.Select(FormatParameter));
                }
                return sb.ToString();
            }

            foreach (var match in result.Commands)
            {
                var cmd = match.Command;
                StringBuilder sb = new StringBuilder()
                    .AppendLine($"Popis: {cmd.Summary}")
                    .AppendLine()
                    .AppendLine($"Základní použití: **{CommandInfoSignature(cmd, HelpModule.CommandInfoSignature.Basic)}**");

                if (cmd.Parameters.Any(t => t.IsOptional))
                    sb.AppendLine($"Plné použití: {CommandInfoSignature(cmd, HelpModule.CommandInfoSignature.Full)}");

                sb.AppendLine();
                if (cmd.Parameters.Any())
                {
                    string parameters = string.Join(", ", cmd.Parameters.Select(ParameterInfoToString));
                    string detailedParameters = string.Join(Environment.NewLine, cmd.Parameters.Select(ParameterInfoToDetailedString));

                    sb
                        .AppendLine($"Parametry: {parameters}")
                        .AppendLine("Popis parametrů:")
                        .AppendLine(detailedParameters);
                }

                builder.AddField(string.Join(", ", cmd.Aliases), sb);
            }

            await ReplyAsync(string.Empty, false, builder.Build());
        }
    }
}
