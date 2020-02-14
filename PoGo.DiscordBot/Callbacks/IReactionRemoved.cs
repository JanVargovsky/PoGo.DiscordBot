using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IReactionRemoved
    {
        Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction);
    }
}
