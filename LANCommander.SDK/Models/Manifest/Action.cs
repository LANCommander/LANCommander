using System;
using System.Collections.Generic;
using LANCommander.SDK.Enums;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Models.Manifest
{
    public class Action : BaseModel
    {
        /// <summary>
        /// The server's id for this action. Absent from manifests written before actions carried
        /// one, and from actions that never came from the server (lobbies, packaging).
        /// </summary>
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Guid? Id { get; set; }

        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public ActionType Type { get; set; }

        /// <summary>
        /// How manifests written before <see cref="Type"/> say which action is primary: read, never
        /// written. Setting it only moves an action into or out of <see cref="ActionType.Primary"/>.
        /// </summary>
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
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

        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public Guid? ToolId { get; set; }
    }
}
