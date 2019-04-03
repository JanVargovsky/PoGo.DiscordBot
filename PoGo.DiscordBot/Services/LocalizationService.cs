using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Resources;
using System.Text;

namespace PoGo.DiscordBot.Services
{
    public class LocalizationService
    {
        protected ResourceManager resourceManager;

          private static readonly Lazy<LocalizationService> lazy
            = new Lazy<LocalizationService>(()
                => new LocalizationService());

        private LocalizationService()
        {
            resourceManager = new ResourceManager("PoGo.DiscordBot",
                Assembly.GetExecutingAssembly());
        }

        public static LocalizationService Instance
        {
            get
            {
                return lazy.Value;
            }
        }

        public string GetStringFromResources(String keyName) => resourceManager.GetString(keyName);
    }
}
