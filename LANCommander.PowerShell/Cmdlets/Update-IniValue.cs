using PeanutButter.INI;
using System;
using System.IO;
using System.Management.Automation;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Update, "IniValue")]
    [OutputType(typeof(string))]
    public class UpdateIniValueCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Section { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public string Key { get; set; }

        [Parameter(Mandatory = true, Position = 2)]
        public string Value { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        public string FilePath { get; set; }

        protected override void ProcessRecord()
        {
            if (File.Exists(FilePath))
            {
                var ini = new INIFile(FilePath);

                ini.SetValue(Section, Key, Value);

                ini.Persist();
            }
        }
    }
}
