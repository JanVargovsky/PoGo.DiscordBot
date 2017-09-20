﻿using Discord.Commands;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    public class InviteModule : ModuleBase<SocketCommandContext>
    {
        readonly ILogger<InviteModule> logger;

        public InviteModule(ILogger<InviteModule> logger)
        {
            this.logger = logger;
        }

        [Command("invite")]
        [Alias("inv")]
        [Summary("Vrátí odkaz s pozvánkou sem na Discord.")]
        public async Task Invite()
        {
            var invites = await Context.Guild.GetInvitesAsync();
            var invite = invites.FirstOrDefault(t => !t.IsTemporary);

            if (invite == null)
            {
                // TODO: call Context.Guild.DefaultChannel instead later on
                var defaultChannel = Context.Guild.TextChannels
                    .OrderBy(c => c.Position)
                    .FirstOrDefault();

                if (defaultChannel == null)
                {
                    await ReplyAsync("Sorry, žádný tu nemám :(");
                    return;
                }

                invite = await defaultChannel.CreateInviteAsync(null);
                logger.LogInformation($"New invite '{invite.Url}' created by user '{Context.User.Id}'");
            }

            await ReplyAsync(invite.Url);
        }
    }
}