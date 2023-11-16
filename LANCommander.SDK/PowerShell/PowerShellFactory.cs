using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.PowerShell
{
    public static class PowerShellFactory
    {
        public static PowerShellScript RunScript()
        {
            return new PowerShellScript();
        }
    }
}
