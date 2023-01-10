using LANCommander.Data.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LANCommander.Models
{
    public class GameViewModel
    {
        public long? IgdbId { get; set; }
        public Game Game { get; set; }
        public List<SelectListItem> Genres { get; set; }
        public List<SelectListItem> Tags { get; set; }
        public List<SelectListItem> Categories { get; set; }
        public List<SelectListItem> Developers { get; set; }
        public List<SelectListItem> Publishers { get; set; }
    }
}
