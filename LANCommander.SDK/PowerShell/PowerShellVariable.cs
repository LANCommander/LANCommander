using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.PowerShell
{
    public class PowerShellVariable
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public Type Type { get; set; }

        public PowerShellVariable(string name, object value, Type type)
        {
            Name = name;
            Value = value;
            Type = type;
        }
    }
}
