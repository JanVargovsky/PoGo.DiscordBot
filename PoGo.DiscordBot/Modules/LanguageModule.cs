﻿using Discord.Commands;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Services;

namespace PoGo.DiscordBot.Modules
{
    //TODO Add a command to support changing the language in the config
    public class LanguageModule : ModuleBase<SocketCommandContext>
    {
        readonly ILogger<LanguageModule> logger;
        readonly ConfigurationService configService;

        public LanguageModule(ILogger<LanguageModule> logger
                                , ConfigurationService configService)
        {
            this.logger = logger;
            this.configService = configService;
        }
    }
}