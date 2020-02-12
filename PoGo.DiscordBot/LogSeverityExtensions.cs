using Discord;
using Microsoft.Extensions.Logging;

namespace PoGo.DiscordBot
{
    public static class LogSeverityExtensions
    {
        public static LogLevel ToLogLevel(this LogSeverity logSeverity) => logSeverity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Critical,
        };
    }
}
