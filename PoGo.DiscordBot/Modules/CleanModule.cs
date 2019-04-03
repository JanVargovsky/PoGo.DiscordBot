using Discord;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public class CleanModule : ModuleBase
    {
        [Command("hardclean", RunMode = RunMode.Async)]
        [Summary("DeleteAllMessagesSummary")]
        public async Task FullClean([Summary("Počet zpráv.")]int count = 10)
        {
            var batchMessages = AsyncEnumerable.ToEnumerable(Context.Channel.GetMessagesAsync(count));
            foreach (var messages in batchMessages)
                await Context.Channel.DeleteMessagesAsync(messages);
        }

        [Command("clean", RunMode = RunMode.Async)]
        [Summary("DeleteYourMessageSummary")]
        public async Task DeleteLastMessagesFromCurrentUser([Summary("Počet zpráv.")]int count = 5)
        {
            await DeleteMessagesAsync(Context.User.Id, count);
        }

        [Command("clean", RunMode = RunMode.Async)]
        [Summary("DeleteLastMessageSummary")]
        public async Task DeleteLastMessages([Summary("Uživatel.")]IUser user,
            [Summary("Počet zpráv.")]int count = 5)
        {
            ulong userId = user != null ? user.Id : Context.User.Id;

            await DeleteMessagesAsync(userId, count);
        }

        async Task DeleteMessagesAsync(ulong userId, int count)
        {
            foreach (var messages in Context.Channel.GetMessagesAsync().ToEnumerable())
            {
                var messagesToDelete = messages.Where(t => t.Author.Id == userId).Take(count);
                if (messagesToDelete != null)
                    await Context.Channel.DeleteMessagesAsync(messagesToDelete.Take(count));
            }
        }
    }
}
