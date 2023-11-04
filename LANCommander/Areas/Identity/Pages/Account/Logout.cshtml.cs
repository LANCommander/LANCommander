// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using LANCommander.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace LANCommander.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<User> SignInManager;
        private readonly ILogger<LogoutModel> Logger;

        public LogoutModel(SignInManager<User> signInManager, ILogger<LogoutModel> logger)
        {
            SignInManager = signInManager;
            Logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            await SignInManager.SignOutAsync();

            return LocalRedirect("/");
        }
    }
}
