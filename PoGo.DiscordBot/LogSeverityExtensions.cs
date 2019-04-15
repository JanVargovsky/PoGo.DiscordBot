using Discord;
using Microsoft.Extensions.Logging;

namespace PoGo.DiscordBot
{
    public static class LogSeverityExtensions
    {
        public static LogLevel ToLogLevel(this LogSeverity logSeverity)
        {
            switch (logSeverity)
            {
                case LogSeverity.Critical:
                    return LogLevel.Critical;
                case LogSeverity.Error:
                    return LogLevel.Error;
                case LogSeverity.Warning:
                    return LogLevel.Warning;
                case LogSeverity.Info:
                    return LogLevel.Information;
                case LogSeverity.Verbose:
                    return LogLevel.Trace;
                case LogSeverity.Debug:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Critical;
            }
        }
    }
}