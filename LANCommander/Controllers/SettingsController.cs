using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly SettingService SettingService;

        public SettingsController(SettingService settingService)
        {
            SettingService = settingService;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(General));
        }

        public IActionResult General()
        {
            var settings = SettingService.GetSettings();

            return View(settings);
        }

        [HttpPost]
        public IActionResult General(LANCommanderSettings settings)
        {
            SettingService.SaveSettings(settings);

            return RedirectToAction(nameof(General));
        }
    }
}
