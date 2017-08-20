namespace PoGo.DiscordBot.Commands
{
    //public class CleanCommand : ModuleBase
    //{
    //    [Command("hardclean", RunMode = RunMode.Async)]
    //    public async Task FullClean()
    //    {
    //        var batchMessages = AsyncEnumerable.ToEnumerable(Context.Channel.GetMessagesAsync());
    //        foreach (var messages in batchMessages)
    //            await Context.Channel.DeleteMessagesAsync(messages);
    //    }

    //    [Command("clean", RunMode = RunMode.Async)]
    //    public async Task DeleteLastMessagesFromCurrentUser(int count = 5)
    //    {
    //        await DeleteMessagesAsync(Context.User.Id, count);
    //    }

    //    [Command("clean", RunMode = RunMode.Async)]
    //    public async Task DeleteLastMessagesFromUser(IUser user, int count = 5)
    //    {
    //        ulong userId = user != null ? user.Id : Context.User.Id;

    //        await DeleteMessagesAsync(userId, count);
    //    }

    //    async Task DeleteMessagesAsync(ulong id, int count)
    //    {
    //        foreach (var messages in AsyncEnumerable.ToEnumerable(Context.Channel.GetMessagesAsync()))
    //        {
    //            var messagesToDelete = messages.Where(t => t.Author.Id == id).Take(count);
    //            if (messagesToDelete != null)
    //                await Context.Channel.DeleteMessagesAsync(messagesToDelete.Take(count));
    //        }
    //    }
    //}
}
