using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace PoGo.DiscordBot.Callbacks;

public interface IReactionRemoved
{
    Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction);
}
