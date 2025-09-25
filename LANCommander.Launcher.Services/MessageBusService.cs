using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public class MessageBusService(ILogger<MessageBusService> logger) : BaseService(logger)
    {
        public delegate Task OnMediaChangedHandler(Media media);
        public event OnMediaChangedHandler OnMediaChanged;

        public void MediaChanged(Media media)
        {
            OnMediaChanged?.Invoke(media);
        }

        public delegate Task OnLibraryFilterAppliedHander();
        public event OnLibraryFilterAppliedHander OnLibraryFilterApplied;

        public void LibraryFilterApplied()
        {
            OnLibraryFilterApplied?.Invoke();
        }
    }
}
