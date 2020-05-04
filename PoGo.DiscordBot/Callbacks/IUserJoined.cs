using System.Threading.Tasks;
using Discord.WebSocket;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IUserJoined
    {
        Task OnUserJoined(SocketGuildUser user);
    }
}
