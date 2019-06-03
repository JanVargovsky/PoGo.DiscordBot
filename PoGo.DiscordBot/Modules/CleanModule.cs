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
        [Summary("Smaže všechny zprávy (omezeno počtem).")]
        public async Task FullClean([Summary("Počet zpráv.")]int count = 10)
        {
            var messages = await Context.Channel.GetMessagesAsync(count).FlattenAsync();
            foreach (var message in messages)
                await Context.Channel.DeleteMessageAsync(message);
        }

        [Command("clean", RunMode = RunMode.Async)]
        [Summary("Smaže tvoje zprávy (omezeno počtem).")]
        public async Task DeleteLastMessagesFromCurrentUser([Summary("Počet zpráv.")]int count = 5)
        {
            await DeleteMessagesAsync(Context.User.Id, count);
        }

        [Command("clean", RunMode = RunMode.Async)]
        [Summary("Smaže zprávy označeného uživatele (omezeno počtem).")]
        public async Task DeleteLastMessages([Summary("Uživatel.")]IUser user,
            [Summary("Počet zpráv.")]int count = 5)
        {
            ulong userId = user != null ? user.Id : Context.User.Id;

            await DeleteMessagesAsync(userId, count);
        }

        async Task DeleteMessagesAsync(ulong userId, int count)
        {
            var messages = (await Context.Channel
                .GetMessagesAsync()
                .FlattenAsync())
                .Where(t => t.Author.Id == userId)
                .Take(count);
            foreach (var message in messages)
                await Context.Channel.DeleteMessageAsync(message);
        }
    }
}
