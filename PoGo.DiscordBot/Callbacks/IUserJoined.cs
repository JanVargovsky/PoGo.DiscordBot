using Discord.WebSocket;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IUserJoined
    {
        Task OnUserJoined(SocketGuildUser user);
    }
}
