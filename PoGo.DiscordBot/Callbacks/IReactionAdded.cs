using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace PoGo.DiscordBot.Callbacks;

public interface IReactionAdded
{
    Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction);
}
