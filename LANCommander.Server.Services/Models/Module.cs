using System;
using System.Collections.Generic;

namespace LANCommander.Server.Models
{
    public enum FunctionVisibility
    {
        Public,
        Private
    }

    public class ModuleFunction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public FunctionVisibility Visibility { get; set; } = FunctionVisibility.Public;
        public string Content { get; set; }
    }

    public class Module
    {
        public string Name { get; set; }
        public string Manifest { get; set; }
        public List<ModuleFunction> Functions { get; set; } = new();
    }

    public record VerbGroup(string Name, IReadOnlyList<string> Verbs);
}
