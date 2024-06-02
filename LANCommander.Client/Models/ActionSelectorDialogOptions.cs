using LANCommander.Client.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Models
{
    public class ActionSelectorDialogOptions
    {
        public IEnumerable<SDK.Models.Action> Actions { get; set; }
        public string Title { get; set; }
        public Game Game { get; set; }
    }
}
