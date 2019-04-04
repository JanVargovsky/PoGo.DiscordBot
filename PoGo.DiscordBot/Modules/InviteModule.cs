using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PoGo.DiscordBot.Properties;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireContext(ContextType.Guild)]
    public class InviteModule : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<InviteModule> logger;

        public InviteModule(ILogger<InviteModule> logger)
        {
            this.logger = logger;
        }

        [Command("invite")]
        [Alias("inv")]
        [Summary("InviteSummary")]
        public async Task Invite()
        {
            IReadOnlyCollection<RestInviteMetadata> invites = await Context.Guild.GetInvitesAsync();
            RestInviteMetadata invite = invites.FirstOrDefault(t => !t.IsTemporary);

            if (invite == null)
            {
                // TODO: call Context.Guild.DefaultChannel instead later on
                SocketTextChannel defaultChannel = Context.Guild.TextChannels
                    .OrderBy(c => c.Position)
                    .FirstOrDefault();

                if (defaultChannel == null)
                {
                    await ReplyAsync("Sorry," +  Resources.NoDefaultChannel + ":(");
                    return;
                }

                invite = await defaultChannel.CreateInviteAsync(null);
                logger.LogInformation($"New invite '{invite.Url}' created by user '{Context.User.Id}'");
            }

            await ReplyAsync(invite.Url);
        }
    }
}