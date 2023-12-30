using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks;

public interface IReady
{
    Task OnReady();
}
