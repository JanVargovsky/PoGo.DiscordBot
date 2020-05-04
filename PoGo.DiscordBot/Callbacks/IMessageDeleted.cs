using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IMessageDeleted
    {
        Task OnMessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel);
    }
}
