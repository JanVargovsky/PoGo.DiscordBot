using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;
using PoGo.DiscordBot.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    public class HelpModule : InteractiveBase<SocketCommandContext>
    {
        private readonly CommandService commandService;
        private readonly IServiceProvider serviceProvider;
        private readonly char prefix;
        //TODO Load the current culture info from guild
        private readonly CultureInfo cultureInfo = CultureInfo.GetCultureInfo("cs-CS");

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
            Dictionary<string, List<string>> groupCommands = new Dictionary<string, List<string>>();

            foreach (ModuleInfo module in commandService.Modules)
            {
                string key = module.Aliases.FirstOrDefault() ?? string.Empty;
                if (!groupCommands.TryGetValue(key, out List<string> commands))
                    groupCommands[key] = commands = new List<string>();

                foreach (CommandInfo cmd in module.Commands)
                {
                    PreconditionResult result = await cmd.CheckPreconditionsAsync(Context, serviceProvider);
                    if (result.IsSuccess)
                    {
                        string s = $"{prefix}{cmd.Aliases[0]}";
                        if (!string.IsNullOrEmpty(cmd.Summary))
                            s += $" ({ Resources.ResourceManager.GetString(cmd.Summary,cultureInfo)})";

                        commands.Add(s);
                    }
                }
            }

            string CommandsToString(IEnumerable<string> commands) =>
                string.Join(Environment.NewLine, commands.OrderBy(t => t));

            List<List<string>> commandPages = new List<List<string>>();
            // Commands with module that has alias equal to "" are without any group
            // and they are on first page without any other group commands
            if (groupCommands.TryGetValue(string.Empty, out List<string> globalCommands))
                commandPages.Add(globalCommands);

            const int MaxCommandsPerPage = 15;
            List<string> currentPageCommands = new List<string>();

            foreach (KeyValuePair<string, List<string>> c in groupCommands.OrderBy(t => t.Key))
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
            List<string> pages = commandPages.Select(CommandsToString).ToList();

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
                    Title = Resources.AllCommands,
                    Pages = pages,
                });
            }
            else if (pages.Count > 0)
                await ReplyAsync($"```{pages[0]}```");
        }

        private enum CommandInfoSignature
        {
            Basic,
            Full
        }

        [Command("help")]
        [Summary("HelpSummary")]
        public async Task Help([Remainder] string command)
        {
            SearchResult result = commandService.Search(Context, command);

            if (!result.IsSuccess)
            {
                string reply = string.Format(Resources.CommandNotFound, command);

                await ReplyAsync(reply);
                return;
            }

            EmbedBuilder builder = new EmbedBuilder()
                .WithColor(Color.Blue);

            string ParameterInfoToString(ParameterInfo info) => !info.IsOptional ? info.Name : $"[{info.Name}]";

            string ParameterInfoToDetailedString(ParameterInfo info)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(info.Name);

                if (!string.IsNullOrEmpty(info.Summary))
                    sb.Append($" - {Resources.ResourceManager.GetString(info.Summary,cultureInfo)}");

                if (info.Type.IsEnum)
                    sb.Append(Resources.PossibleValues + $" ({string.Join(" | ", Enum.GetNames(info.Type))})!");

                if (info.IsOptional)
                    sb.Append(Resources.Optional + $" ({info.DefaultValue})");

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
                    IEnumerable<ParameterInfo> parameters = ci.Parameters.AsEnumerable();

                    if (signature == HelpModule.CommandInfoSignature.Basic)
                        parameters = parameters.Where(pi => !pi.IsOptional);

                    sb.Append(' ')
                        .AppendJoin(' ', parameters.Select(FormatParameter));
                }
                return sb.ToString();
            }

            foreach (CommandMatch match in result.Commands)
            {
                CommandInfo cmd = match.Command;
                StringBuilder sb = new StringBuilder()
                    .Append(Resources.Description).Append(':').AppendLine(cmd.Summary)
                    .AppendLine()
                    .Append(Resources.BasicUse).Append(":**").Append(CommandInfoSignature(cmd, HelpModule.CommandInfoSignature.Basic)).AppendLine("**");

                if (cmd.Parameters.Any(t => t.IsOptional))
                    sb.AppendLine(Resources.FullUse + $": {CommandInfoSignature(cmd, HelpModule.CommandInfoSignature.Full)}");

                sb.AppendLine();
                if (cmd.Parameters.Count > 0)
                {
                    string parameters = string.Join(", ", cmd.Parameters.Select(ParameterInfoToString));
                    string detailedParameters = string.Join(Environment.NewLine, cmd.Parameters.Select(ParameterInfoToDetailedString));

                    sb
                        .Append(Resources.Parameters).Append(": ").AppendLine(parameters)
                        .AppendLine(Resources.ParameterDescription)
                        .AppendLine(detailedParameters);
                }

                builder.AddField(Resources.Command + $"{(cmd.Aliases.Count > 1 ? "y" : "")}: {string.Join(", ", cmd.Aliases)}", sb);
            }

            await ReplyAsync(string.Empty, false, builder.Build());
        }
    }
}