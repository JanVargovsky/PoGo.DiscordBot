using System.Threading.Tasks;
using Discord.WebSocket;

namespace PoGo.DiscordBot.Callbacks;

public interface IInteractionCreated
{
    Task OnInteractionCreated(SocketInteraction interaction);
}
