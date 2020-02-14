using System;
using System.Threading.Tasks;

namespace PoGo.DiscordBot.Callbacks
{
    public interface IDisconnected
    {
        Task OnDisconnected(Exception exception);
    }
}
