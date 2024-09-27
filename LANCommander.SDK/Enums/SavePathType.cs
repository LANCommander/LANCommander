using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace LANCommander.SDK.Enums
{
    public enum SavePathType
    {
        [Display(Name = "File / Folder")]
        File,
        [Display(Name = "Registry Key")]
        Registry
    }
}
