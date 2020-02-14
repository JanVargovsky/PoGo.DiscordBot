using Discord.WebSocket;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IMessageReceived
    {
        Task OnMessageReceived(SocketMessage message);
    }
}
