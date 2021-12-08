using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Microsoft.Extensions.Options;
using PoGo.DiscordBot.Configuration.Options;

namespace PoGo.DiscordBot.Modules;

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
    [Summary("Vypíše seznam příkazů.")]
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
            if (c.Key == string.Empty) continue;

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
        if (currentPageCommands.Any())
            commandPages.Add(currentPageCommands);
        var pages = commandPages.Select(CommandsToString).ToList();

        if (pages.Count > 1)
            await PagedReplyAsync(new PaginatedMessage
            {
                Color = Color.Blue,
                Options = new PaginatedAppearanceOptions
                {
                    JumpDisplayOptions = JumpDisplayOptions.Never,
                    DisplayInformationIcon = false,
                    Timeout = TimeSpan.FromMinutes(1),
                },
                Title = "Dostupné příkazy",
                Pages = pages,
            });
        else if (pages.Any())
            await ReplyAsync($"```{pages.First()}```");
    }

    [Command("help")]
    [Summary("Vypíše nápovědu pro konkrétní příkaz.")]
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
                .Append(ci.Aliases.First());

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

            builder.AddField($"Příkaz{(cmd.Aliases.Count > 1 ? "y" : "")}: {string.Join(", ", cmd.Aliases)}", sb);
        }

        await ReplyAsync(string.Empty, false, builder.Build());
    }

    private enum CommandInfoSignature
    {
        Basic,
        Full
    }
}
