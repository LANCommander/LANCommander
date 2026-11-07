using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    public class Action : KeyedModel
    {
        public Guid GameId { get; set; }
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public ActionType Type { get; set; }

        /// <summary>
        /// Read from servers that predate <see cref="Type"/>, which sent only this flag; never written.
        /// Setting it only moves an action into or out of <see cref="ActionType.Primary"/>.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsPrimaryAction
        {
            get => null;
            set
            {
                if (value.HasValue)
                    Type = ActionTypes.FromPrimaryFlag(value.Value, Type);
            }
        }

        public int SortOrder { get; set; }
        public string OptionOverrides { get; set; }
        public RuntimePlatform Platforms { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }
}
