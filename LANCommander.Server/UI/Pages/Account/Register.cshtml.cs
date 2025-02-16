// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.UI.Pages.Account
{
    public enum RegistrationType
    {
        Basic,
        AuthenticationProvider,
    }
    
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> SignInManager;
        private readonly UserManager<User> UserManager;
        private readonly IUserStore<User> UserStore;
        private readonly ILogger<RegisterModel> Logger;
        private readonly RoleManager<Role> RoleManager;
        private readonly UserCustomFieldService UserCustomFieldService;

        public RegisterModel(
            UserManager<User> userManager,
            IUserStore<User> userStore,
            SignInManager<User> signInManager,
            ILogger<RegisterModel> logger,
            RoleManager<Role> roleManager,
            UserCustomFieldService userCustomFieldService)
        {
            UserManager = userManager;
            UserStore = userStore;
            SignInManager = signInManager;
            Logger = logger;
            RoleManager = roleManager;
            UserCustomFieldService = userCustomFieldService;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public LANCommander.Server.Models.RegisterModel Model { get; set; } = new();
        
        public AuthenticationProvider AuthenticationProvider { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }
        
        public string ScreenshotUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }
        
        public Services.Models.Settings Settings = SettingService.GetSettings();

        public async Task OnGetAsync(string returnUrl = null, string provider = null)
        {
            ReturnUrl = returnUrl;

            if (!String.IsNullOrWhiteSpace(provider) && User.Identity != null && User.Identity.IsAuthenticated)
            {
                Model.RegistrationType = RegistrationType.AuthenticationProvider;
                
                Model.UserName = User.Identity.Name ?? string.Empty;
                Model.Email = User.FindFirst(ClaimTypes.Email)?.Value;
                Model.Password = Guid.Empty.ToString();
                Model.PasswordConfirmation = Model.Password;
                
                AuthenticationProvider = Settings.Authentication.AuthenticationProviders.FirstOrDefault(p => p.Slug == provider);
            }
            else
            {
                Model.RegistrationType = RegistrationType.Basic;
            }
            
            var screenshots = Directory.GetFiles(Path.Combine("wwwroot", "static", "login"), "*.jpg");

            ScreenshotUrl = screenshots[new Random().Next(0, screenshots.Length - 1)].Replace("wwwroot", "").Replace(Path.DirectorySeparatorChar, '/');
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null, string provider = null)
        {
            returnUrl ??= Url.Content("~/");
            
            ExternalLogins = (await SignInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            
            if (ModelState.IsValid)
            {
                var user = CreateUser();

                if (!Settings.Authentication.RequireApproval)
                {
                    user.Approved = true;
                    user.ApprovedOn = DateTime.UtcNow;
                }

                await UserStore.SetUserNameAsync(user, Model.UserName, CancellationToken.None);

                IdentityResult result;

                if (Model.RegistrationType == RegistrationType.AuthenticationProvider)
                    result = await UserManager.CreateAsync(user);
                else
                    result = await UserManager.CreateAsync(user, Model.Password);

                if (result.Succeeded)
                {
                    Logger.LogInformation("User created a new account.");

                    if (Settings.Roles.DefaultRoleId != Guid.Empty)
                    {
                        var defaultRole = await RoleManager.FindByIdAsync(Settings.Roles.DefaultRoleId.ToString());

                        if (defaultRole != null)
                            await UserManager.AddToRoleAsync(user, defaultRole.Name);
                    }
                    
                    // Registering using SSO
                    if (Model.RegistrationType == RegistrationType.AuthenticationProvider)
                    {
                        AuthenticationProvider = Settings.Authentication.AuthenticationProviders.FirstOrDefault(p => p.Slug == provider);
                        
                        await UserCustomFieldService.AddAsync(new UserCustomField
                        {
                            UserId = user.Id,
                            Name = AuthenticationProvider.GetCustomFieldName(),
                            Value = User.FindFirst(ClaimTypes.NameIdentifier).Value,
                        });
                    }

                    var userId = await UserManager.GetUserIdAsync(user);
                    var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await SignInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private User CreateUser()
        {
            try
            {
                return Activator.CreateInstance<User>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(User)}'. " +
                    $"Ensure that '{nameof(User)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }
    }
}
