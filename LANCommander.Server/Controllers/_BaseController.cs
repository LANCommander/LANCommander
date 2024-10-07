using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LANCommander.Server.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ILogger Logger;
        protected readonly Settings Settings;

        public BaseController(ILogger logger)
        {
            Logger = logger;
            Settings = SettingService.GetSettings();
        }

        protected void Alert(string message, string type = "info", bool dismissable = true)
        {
            List<AlertViewModel> alerts;

            try
            {
                alerts = JsonSerializer.Deserialize<List<AlertViewModel>>((string)TempData["Alerts"]);
            }
            catch
            {
                alerts = new List<AlertViewModel>();
            }

            alerts.Add(new AlertViewModel()
            {
                Message = message,
                Type = type,
                Dismissable = dismissable
            });

            TempData["Alerts"] = JsonSerializer.Serialize(alerts);
        }
    }
}
