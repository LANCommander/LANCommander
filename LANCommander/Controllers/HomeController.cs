using ByteSizeLib;
using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace LANCommander.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DatabaseContext Context;

        public HomeController(ILogger<HomeController> logger, DatabaseContext context)
        {
            Context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var drives = DriveInfo.GetDrives();
            var root = Path.GetPathRoot(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var model = new DashboardViewModel();

            model.TotalStorageSize = ByteSize.FromBytes(drives.Where(d => d.IsReady && d.Name == root).Sum(d => d.TotalSize));
            model.TotalAvailableFreeSpace = ByteSize.FromBytes(drives.Where(d => d.IsReady && d.Name == root).Sum(d => d.AvailableFreeSpace));
            model.TotalUploadDirectorySize = ByteSize.FromBytes(new DirectoryInfo("Upload").EnumerateFiles().Sum(f => f.Length));

            using (Repository<Game> repo = new Repository<Game>(Context, HttpContext))
            {
                model.GameCount = repo.Get(g => true).Count();
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}