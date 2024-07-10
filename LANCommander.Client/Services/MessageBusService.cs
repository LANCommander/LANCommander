using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class MessageBusService
    {
        public delegate Task OnMediaChangedHandler(Media media);
        public event OnMediaChangedHandler OnMediaChanged;

        public void MediaChanged(Media media)
        {
            OnMediaChanged?.Invoke(media);
        } 
    }
}
