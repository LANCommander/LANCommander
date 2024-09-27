using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LANCommander.SDK.PowerShell
{
    public class PowerShellVariableList : List<PowerShellVariable>
    {
        public T GetValue<T>(string variableName)
        {
            var variable = this.FirstOrDefault(v => v.Name == variableName);

            return (T)variable.Value;
        }
    }

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
