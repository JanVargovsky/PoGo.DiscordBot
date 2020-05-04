using System.Threading.Tasks;
using Discord.WebSocket;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IMessageReceived
    {
        Task OnMessageReceived(SocketMessage message);
    }
}
