// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using LANCommander.Server.Services;
using LANCommander.Server.Extensions;
using LANCommander.Server.Data;
using LANCommander.Server.Models;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Components;
using ZiggyCreatures.Caching.Fusion;
using User = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.UI.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<User> SignInManager;
        private readonly UserService UserService;
        private readonly RoleService RoleService;
        private readonly IFusionCache Cache;
        private readonly ILogger<LoginModel> Logger;

        public LoginModel(
            SignInManager<User> signInManager,
            UserService userService,
            RoleService roleService,
            IFusionCache cache,
            ILogger<LoginModel> logger)
        {
            SignInManager = signInManager;
            UserService = userService;
            RoleService = roleService;
            Cache = cache;
            Logger = logger;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public Models.LoginModel Model { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }
        
        public AuthToken Token { get; set; }
        
        public string ScreenshotUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null, string code = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;

            if (DatabaseContext.Provider == Data.Enums.DatabaseProvider.Unknown)
                return Redirect("/FirstTimeSetup");

            var administrators = await RoleService.GetUsersAsync(RoleService.AdministratorRoleName);

            if (administrators == null || !administrators.Any())
                return Redirect("/FirstTimeSetup");

            if (!String.IsNullOrWhiteSpace(code))
            {
                var token = await Cache.GetOrDefaultAsync<AuthToken>($"AuthToken/{code}", null);

                if (token != null)
                {
                    Token = token;
                }
            }
            
            var screenshots = Directory.GetFiles(Path.Combine("wwwroot", "static", "login"), "*.jpg");

            if (screenshots.Any())
                ScreenshotUrl = screenshots[new Random().Next(0, screenshots.Length - 1)].Replace("wwwroot", "").Replace(Path.DirectorySeparatorChar, '/');

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null, string provider = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!String.IsNullOrWhiteSpace(provider) && await HttpContext.IsProviderSupportedAsync(provider))
            {
                var properties = new AuthenticationProperties(new Dictionary<string, string>()
                {
                    { "Action", AuthenticationProviderActionType.Login.ToString() }
                });

                properties.RedirectUri = returnUrl;
                
                return Challenge(properties, provider);
            }
            
            var screenshots = Directory.GetFiles(Path.Combine("wwwroot", "static", "login"), "*.jpg");

            if (screenshots.Any())
                ScreenshotUrl = screenshots[new Random().Next(0, screenshots.Length - 1)].Replace("wwwroot", "").Replace(Path.DirectorySeparatorChar, '/');

            if (ModelState.IsValid)
            {
                var settings = SettingService.GetSettings();

                if (settings.Authentication.RequireApproval)
                {
                    var user = await UserService.GetAsync(Model.Username);

                    if (user != null && !user.Approved && !(await UserService.IsInRoleAsync(user, RoleService.AdministratorRoleName)))
                    {
                        ModelState.AddModelError(string.Empty, "Your account must be approved by an administrator.");
                        return Page();
                    }
                }

                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await SignInManager.PasswordSignInAsync(Model.Username, Model.Password, Model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    Logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Model.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    Logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
