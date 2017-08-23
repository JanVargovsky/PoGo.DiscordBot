using Discord;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Modules
{
    [RequireUserPermission(GuildPermission.Administrator)]
    public class CleanModule : ModuleBase
    {
        [Command("hardclean", RunMode = RunMode.Async)]
        public async Task FullClean(int count = 10)
        {
            var batchMessages = AsyncEnumerable.ToEnumerable(Context.Channel.GetMessagesAsync(count));
            foreach (var messages in batchMessages)
                await Context.Channel.DeleteMessagesAsync(messages);
        }

        [Command("clean", RunMode = RunMode.Async)]
        public async Task DeleteLastMessagesFromCurrentUser(int count = 5)
        {
            await DeleteMessagesAsync(Context.User.Id, count);
        }

        [Command("clean", RunMode = RunMode.Async)]
        public async Task DeleteLastMessages(IUser user, int count = 5)
        {
            ulong userId = user != null ? user.Id : Context.User.Id;

            await DeleteMessagesAsync(userId, count);
        }

        async Task DeleteMessagesAsync(ulong userId, int count)
        {
            foreach (var messages in AsyncEnumerable.ToEnumerable(Context.Channel.GetMessagesAsync()))
            {
                var messagesToDelete = messages.Where(t => t.Author.Id == userId).Take(count);
                if (messagesToDelete != null)
                    await Context.Channel.DeleteMessagesAsync(messagesToDelete.Take(count));
            }
        }
    }
}
