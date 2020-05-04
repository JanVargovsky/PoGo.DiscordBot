using System.Threading.Tasks;
using Discord.WebSocket;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IGuildAvailable
    {
        Task OnGuildAvailable(SocketGuild guild);
    }
}
