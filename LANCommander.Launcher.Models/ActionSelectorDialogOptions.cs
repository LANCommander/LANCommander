using LANCommander.Launcher.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public class ActionSelectorDialogOptions
    {
        public IEnumerable<SDK.Models.Manifest.Action> Actions { get; set; }
        public string Title { get; set; }
        public Game Game { get; set; }
    }
}
