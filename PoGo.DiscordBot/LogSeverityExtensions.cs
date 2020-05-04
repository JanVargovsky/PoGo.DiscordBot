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
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Critical,
        };

        public static LogSeverity ToLogLevel(this LogLevel logLevel) => logLevel switch
        {
            LogLevel.Critical => LogSeverity.Critical,
            LogLevel.Error => LogSeverity.Error,
            LogLevel.Warning => LogSeverity.Warning,
            LogLevel.Information => LogSeverity.Info,
            LogLevel.Debug => LogSeverity.Verbose,
            LogLevel.Trace => LogSeverity.Debug,
            _ => LogSeverity.Critical,
        };
    }
}
