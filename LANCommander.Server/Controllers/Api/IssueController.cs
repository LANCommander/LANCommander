using AutoMapper;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class IssueController : BaseApiController
    {
        private readonly GameService GameService;
        private readonly IssueService IssueService;
        private readonly UserManager<User> UserManager;

        public IssueController(
            ILogger<IssueController> logger,
            GameService gameService,
            IssueService issueService,
            UserManager<User> userManager) : base(logger)
        {
            GameService = gameService;
            IssueService = issueService;
            UserManager = userManager;
        }

        [HttpPost("Open")]
        public async Task<bool> Open(SDK.Models.Issue issueRequest)
        {
            try
            {
                var game = await GameService.Get(issueRequest.GameId);
                var user = await UserManager.FindByNameAsync(User.Identity.Name);

                if (game != null)
                {
                    var issue = new Issue()
                    {
                        Game = game,
                        Description = issueRequest.Description
                    };

                    issue = await IssueService.Add(issue);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not open new issue");
            }

            return false;
        }
    }
}
