using LANCommander.Data.Models;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class SettingsController : BaseController
    {
        private readonly SettingService SettingService;
        private readonly UserManager<User> UserManager;

        public SettingsController(SettingService settingService, UserManager<User> userManager)
        {
            SettingService = settingService;
            UserManager = userManager;
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

        public async Task<IActionResult> Users()
        {
            var users = new List<UserViewModel>();

            foreach (var user in UserManager.Users)
            {
                var savePath = Path.Combine("Save", user.Id.ToString());
                long saveSize = 0;

                if (Directory.Exists(savePath))
                    saveSize = new DirectoryInfo(savePath).EnumerateFiles().Sum(f => f.Length);

                users.Add(new UserViewModel()
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Roles = await UserManager.GetRolesAsync(user),
                    SavesSize = saveSize
                });
            }

            return View(users);
        }

        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var user = await UserManager.FindByIdAsync(id.ToString());
            var admins = await UserManager.GetUsersInRoleAsync("Administrator");

            if (user.UserName == HttpContext.User.Identity.Name)
            {
                Alert("You cannot delete yourself!", "danger");

                return RedirectToAction(nameof(Users));
            }

            if (admins.Count == 1 && admins.First().Id == id)
            {
                Alert("You cannot delete the only admin user!", "danger");

                return RedirectToAction(nameof(Users));
            }

            try
            {
                await UserManager.DeleteAsync(user);

                Alert("User successfully deleted!", "success");

                return RedirectToAction(nameof(Users));
            }
            catch
            {
                Alert("User could not be deleted!", "danger");

                return RedirectToAction(nameof(Users));
            }
        }

        public async Task<IActionResult> PromoteUser(Guid id)
        {
            var user = await UserManager.FindByIdAsync(id.ToString());
            
            try
            {
                await UserManager.AddToRoleAsync(user, "Administrator");

                Alert("User promoted to administrator!", "success");

                return RedirectToAction(nameof(Users));
            }
            catch (Exception ex)
            {
                Alert("User could not be promoted!", "danger");

                return RedirectToAction(nameof(Users));
            }
        }

        public async Task<IActionResult> DemoteUser(Guid id)
        {
            var user = await UserManager.FindByIdAsync(id.ToString());
            var admins = await UserManager.GetUsersInRoleAsync("Administrator");

            if (user.UserName == HttpContext.User.Identity.Name)
            {
                Alert("You cannot demote yourself!", "danger");

                return RedirectToAction(nameof(Users));
            }

            try
            {
                await UserManager.RemoveFromRoleAsync(user, "Administrator");

                Alert("User successfully demoted!", "success");

                return RedirectToAction(nameof(Users));
            }
            catch
            {
                Alert("User could not be demoted!", "danger");

                return RedirectToAction(nameof(Users));
            }
        }
    }
}
