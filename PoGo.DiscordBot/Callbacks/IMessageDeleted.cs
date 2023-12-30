using System.Threading.Tasks;
using Discord;

namespace PoGo.DiscordBot.Callbacks;

public interface IMessageDeleted
{
    Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel);
}
