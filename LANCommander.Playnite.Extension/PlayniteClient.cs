using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    public class PlayniteClient : LibraryClient
    {
        public override bool IsInstalled => true;

        public override void Open()
        {

            System.Diagnostics.Process.Start("https://localhost:7087");
        }
    }
}
