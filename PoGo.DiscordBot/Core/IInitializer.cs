using System.Threading.Tasks;

namespace PoGo.DiscordBot.Core;

public interface IInitializer
{
    ValueTask InitializeAsync();
}
