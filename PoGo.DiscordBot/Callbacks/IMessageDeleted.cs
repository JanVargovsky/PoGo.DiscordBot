using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IMessageDeleted
    {
        Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel);
    }
}
