using Discord.WebSocket;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IGuildAvailable
    {
        Task OnGuildAvailable(SocketGuild guild);
    }
}
