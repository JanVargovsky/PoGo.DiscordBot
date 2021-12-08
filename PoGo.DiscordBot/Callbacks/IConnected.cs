using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks;

public interface IConnected
{
    Task OnConnected();
}
