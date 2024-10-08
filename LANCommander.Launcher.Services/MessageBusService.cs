﻿using LANCommander.Launcher.Data.Models;
using Microsoft.Extensions.Logging;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class MessageBusService : BaseService
    {
        public delegate Task OnMediaChangedHandler(Media media);
        public event OnMediaChangedHandler OnMediaChanged;

        public MessageBusService(SDK.Client client, ILogger<MessageBusService> logger) : base(client, logger) { }

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
