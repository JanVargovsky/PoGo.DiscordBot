using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class HelpModule : InteractiveBase<SocketCommandContext>
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
        [Summary("ListCommandsSummary")]
        public async Task Help()
        {
            var groupCommands = new Dictionary<string, List<string>>();

            foreach (var module in commandService.Modules)
            {
                string key = module.Aliases.FirstOrDefault() ?? string.Empty;
                if (!groupCommands.TryGetValue(key, out var commands))
                    groupCommands[key] = commands = new List<string>();

                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context, serviceProvider);
                    if (result.IsSuccess)
                    {
                        string s = $"{prefix}{cmd.Aliases.First()}";
                        if (!string.IsNullOrEmpty(cmd.Summary))
                            s += $" ({ cmd.Summary})";

                        commands.Add(s);
                    }
                }
            }

            string CommandsToString(IEnumerable<string> commands) =>
                string.Join(Environment.NewLine, commands.OrderBy(t => t));

            var commandPages = new List<List<string>>();
            // Commands with module that has alias equal to "" are without any group
            // and they are on first page without any other group commands
            if (groupCommands.TryGetValue(string.Empty, out var globalCommands))
                commandPages.Add(globalCommands);

            const int MaxCommandsPerPage = 15;
            List<string> currentPageCommands = new List<string>();

            foreach (var c in groupCommands.OrderBy(t => t.Key))
            {
                if (c.Key?.Length == 0) continue;

                // future hint for division
                // c.Value.Count / MaxCommandsPerPage > 1 ... then divide it into N pages

                if (currentPageCommands.Count + c.Value.Count > MaxCommandsPerPage)
                {
                    // We cannot add more commands
                    commandPages.Add(currentPageCommands);
                    currentPageCommands = new List<string>(c.Value);
                    continue;
                }

                currentPageCommands.AddRange(c.Value);
            }
            if (currentPageCommands.Count > 0)
                commandPages.Add(currentPageCommands);
            var pages = commandPages.Select(CommandsToString).ToList();

            if (pages.Count > 1)
            {
                await PagedReplyAsync(new PaginatedMessage
                {
                    Color = Color.Blue,
                    Options = new PaginatedAppearanceOptions
                    {
                        JumpDisplayOptions = JumpDisplayOptions.Never,
                        DisplayInformationIcon = false,
                        Timeout = TimeSpan.FromMinutes(1),
                    },
                    Title = LocalizationService.Instance.GetStringFromResources("AvailableCommands"),
                    Pages = pages,
                });
            }
            else if (pages.Count > 0)
                await ReplyAsync($"```{pages.First()}```");
        }

        private enum CommandInfoSignature
        {
            Basic,
            Full
        }

        [Command("help")]
        [Summary("Vypíše nápovědu pro konkrétní příkaz.")]
        public async Task Help([Remainder] string command)
        {
            var result = commandService.Search(Context, command);

            if (!result.IsSuccess)
            {
                string reply = String.Format(LocalizationService.Instance.GetStringFromResources("CommandNotFound"),command);

                await ReplyAsync(reply);
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
                    sb.Append($" - {LocalizationService.Instance.GetStringFromResources(info.Summary)}");

                if (info.Type.IsEnum)
                    sb.Append(LocalizationService.Instance.GetStringFromResources("PossibleValues") + $" ({string.Join(" | ", Enum.GetNames(info.Type))})!");

                if (info.IsOptional)
                    sb.Append(LocalizationService.Instance.GetStringFromResources("Optional") +  $" ({info.DefaultValue})");

                return sb.ToString();
            }

            string CommandInfoSignature(CommandInfo ci, CommandInfoSignature signature)
            {
                StringBuilder sb = new StringBuilder()
                    .Append(prefix)
                    .Append(ci.Aliases[0]);

                string FormatParameter(ParameterInfo pi) => $"<{pi.Name}>";

                if (ci.Parameters.Count > 0)
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
                    .Append(LocalizationService.Instance.GetStringFromResources("Description")).Append(':').AppendLine(cmd.Summary)
                    .AppendLine()
                    .Append(LocalizationService.Instance.GetStringFromResources("BasicUse")).Append(":**").Append(CommandInfoSignature(cmd, HelpModule.CommandInfoSignature.Basic)).AppendLine("**");

                if (cmd.Parameters.Any(t => t.IsOptional))
                    sb.AppendLine(LocalizationService.Instance.GetStringFromResources("FullUse") + $": {CommandInfoSignature(cmd, HelpModule.CommandInfoSignature.Full)}");

                sb.AppendLine();
                if (cmd.Parameters.Count > 0)
                {
                    string parameters = string.Join(", ", cmd.Parameters.Select(ParameterInfoToString));
                    string detailedParameters = string.Join(Environment.NewLine, cmd.Parameters.Select(ParameterInfoToDetailedString));

                    sb
                        .Append(LocalizationService.Instance.GetStringFromResources("Parameters")).Append(": ").AppendLine(parameters)
                        .AppendLine(LocalizationService.Instance.GetStringFromResources("ParameterDescription"))
                        .AppendLine(detailedParameters);
                }

                builder.AddField(LocalizationService.Instance.GetStringFromResources("Command") + $"{(cmd.Aliases.Count > 1 ? "y" : "")}: {string.Join(", ", cmd.Aliases)}", sb);
            }

            await ReplyAsync(string.Empty, false, builder.Build());
        }
    }
}
