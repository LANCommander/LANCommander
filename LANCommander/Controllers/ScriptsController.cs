using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LANCommander.Data;
using LANCommander.Data.Models;
using Microsoft.AspNetCore.Authorization;
using LANCommander.Models;
using LANCommander.Services;

namespace LANCommander.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ScriptsController : BaseController
    {
        private readonly GameService GameService;
        private readonly ScriptService ScriptService;

        public ScriptsController(GameService gameService, ScriptService scriptService)
        {
            GameService = gameService;
            ScriptService = scriptService;
        }

        public async Task<IActionResult> Add(Guid? id)
        {
            var game = await GameService.Get(id.GetValueOrDefault());

            if (game == null)
                return NotFound();

            var script = new Script()
            {
                GameId = game.Id,
                Game = game
            };

            return View(script);
        }

        [HttpPost]
        public async Task<IActionResult> Add(Script script)
        {
            script.Id = Guid.Empty;

            if (ModelState.IsValid)
            {
                script = await ScriptService.Add(script);

                return RedirectToAction("Edit", "Games", new { id = script.GameId });
            }

            return View(script);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            var script = await ScriptService.Get(id.GetValueOrDefault());

            if (script == null)
                return NotFound();

            return View(script);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Script script)
        {
            if (ModelState.IsValid)
            {
                await ScriptService.Update(script);

                Alert("The script has been saved!", "success");

                return RedirectToAction("Edit", "Games", new { id = script.GameId });
            }

            script.Game = await GameService.Get(script.GameId.GetValueOrDefault());

            return View(script);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            var script = await ScriptService.Get(id.GetValueOrDefault());

            if (script == null)
                return NotFound();

            var gameId = script.GameId;

            await ScriptService.Delete(script);

            return RedirectToAction("Edit", "Games", new { id = gameId });
        }
    }
}
