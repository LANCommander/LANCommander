using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace LANCommander.Server.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ILogger Logger;
        protected readonly SettingsProvider<Settings.Settings> SettingsProvider;

        public BaseController(ILogger logger, SettingsProvider<Settings.Settings> settingsProvider)
        {
            Logger = logger;
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
