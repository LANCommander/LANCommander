#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.Server.Data;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using User = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.UI.Pages.Account
{
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<User> SignInManager;
        private readonly RoleService RoleService;
        private readonly ILogger<ExternalLoginModel> Logger;
        private readonly IOptions<Server.Settings.Settings> Settings;

        public ExternalLoginModel(
            SignInManager<User> signInManager,
            RoleService roleService,
            ILogger<ExternalLoginModel> logger,
            IOptions<Server.Settings.Settings> settings)
        {
            SignInManager = signInManager;
            RoleService = roleService;
            Logger = logger;
            Settings = settings;
        }

        public string ReturnUrl { get; set; }

        public string ScreenshotUrl { get; set; }

        public IEnumerable<AuthenticationProvider> Providers { get; set; } = new List<AuthenticationProvider>();

        public async Task<IActionResult> OnGetAsync(string returnUrl = null, string error = null)
        {
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            if (DatabaseContext.Provider == DatabaseProvider.Unknown)
                return Redirect("/FirstTimeSetup");

            var administratorRole = await RoleService
                .Include(r => r.UserRoles)
                .FirstOrDefaultAsync(r => r.Name == RoleService.AdministratorRoleName);

            if (administratorRole == null || administratorRole.UserRoles != null && !administratorRole.UserRoles.Any())
                return Redirect("/FirstTimeSetup");

            if (!String.IsNullOrEmpty(error))
                ModelState.AddModelError(string.Empty, error);

            Providers = HttpContext.GetExternalProviders()?.ToList() ?? new List<AuthenticationProvider>();

            var providerCount = Providers.Count();

            // Fall back to the standard login page if auto-redirect is disabled or there
            // are no providers to redirect to.
            if (!Settings.Value.Server.Authentication.AutoRedirectToProvider || providerCount == 0)
                return Redirect($"/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");

            // A single provider can be challenged directly without showing the list.
            if (providerCount == 1)
                return ChallengeProvider(Providers.First().Slug, returnUrl);

            LoadScreenshot();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null, string provider = null)
        {
            returnUrl ??= Url.Content("~/");

            if (returnUrl == "/Logout")
                returnUrl = "/";

            if (!String.IsNullOrWhiteSpace(provider) && await HttpContext.IsProviderSupportedAsync(provider))
                return ChallengeProvider(provider, returnUrl);

            return Redirect($"/ExternalLogin?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        private IActionResult ChallengeProvider(string provider, string returnUrl)
        {
            var properties = new AuthenticationProperties(new Dictionary<string, string>()
            {
                { "Action", AuthenticationProviderActionType.Login.ToString() }
            });

            properties.RedirectUri = returnUrl;

            return Challenge(properties, provider);
        }

        private void LoadScreenshot()
        {
            var screenshots = Directory.GetFiles(Path.Combine("wwwroot", "static", "login"), "*.jpg");

            if (screenshots.Any())
                ScreenshotUrl = screenshots[new Random().Next(0, screenshots.Length - 1)].Replace("wwwroot", "").Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
