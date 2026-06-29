using System.IO.Compression;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using LANCommander.SDK;
using LANCommander.Server.Models;

namespace LANCommander.Server.Services
{
    public sealed class ModuleService(SettingsProvider<Settings.Settings> settingsProvider)
    {
        private static readonly Regex FunctionNamePattern =
            new(@"^[A-Za-z][A-Za-z0-9]*-[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);

        // Standard set of approved PowerShell verbs (Get-Verb), grouped by verb group.
        private static readonly IReadOnlyList<VerbGroup> ApprovedVerbGroups = new[]
        {
            new VerbGroup("Common", new[]
            {
                "Add", "Clear", "Close", "Copy", "Enter", "Exit", "Find", "Format", "Get", "Hide", "Join",
                "Lock", "Move", "New", "Open", "Optimize", "Pop", "Push", "Redo", "Remove", "Rename", "Reset",
                "Resize", "Search", "Select", "Set", "Show", "Skip", "Split", "Step", "Switch", "Undo", "Unlock", "Watch",
            }),
            new VerbGroup("Communications", new[]
            {
                "Connect", "Disconnect", "Read", "Receive", "Send", "Write",
            }),
            new VerbGroup("Data", new[]
            {
                "Backup", "Checkpoint", "Compare", "Compress", "Convert", "ConvertFrom", "ConvertTo", "Dismount",
                "Edit", "Expand", "Export", "Group", "Import", "Initialize", "Limit", "Merge", "Mount", "Out",
                "Publish", "Restore", "Save", "Sync", "Unpublish", "Update",
            }),
            new VerbGroup("Diagnostic", new[]
            {
                "Debug", "Measure", "Ping", "Repair", "Resolve", "Test", "Trace",
            }),
            new VerbGroup("Lifecycle", new[]
            {
                "Approve", "Assert", "Build", "Complete", "Confirm", "Deny", "Deploy", "Disable", "Enable",
                "Install", "Invoke", "Register", "Request", "Restart", "Resume", "Start", "Stop", "Submit",
                "Suspend", "Uninstall", "Unregister", "Wait",
            }),
            new VerbGroup("Security", new[]
            {
                "Block", "Grant", "Protect", "Revoke", "Unblock", "Unprotect",
            }),
            new VerbGroup("Other", new[]
            {
                "Use",
            }),
        };

        private static readonly HashSet<string> ApprovedVerbs =
            new(ApprovedVerbGroups.SelectMany(g => g.Verbs), StringComparer.OrdinalIgnoreCase);

        private string GetStoragePath()
        {
            var storagePath = settingsProvider.CurrentValue.Server.Scripts.Modules.StoragePath;

            if (string.IsNullOrWhiteSpace(storagePath))
            {
                storagePath = AppPaths.GetConfigPath("Modules");

                settingsProvider.Update(s =>
                {
                    s.Server.Scripts.Modules.StoragePath = storagePath;
                });
            }

            if (!Directory.Exists(storagePath))
                Directory.CreateDirectory(storagePath);

            return storagePath;
        }

        private string GetModuleDirectory(string name) => Path.Combine(GetStoragePath(), name);

        private static string GetManifestPath(string moduleDirectory, string name) =>
            Path.Combine(moduleDirectory, $"{name}.psd1");

        private static string GetLoaderPath(string moduleDirectory, string name) =>
            Path.Combine(moduleDirectory, $"{name}.psm1");

        private static string GetVisibilityDirectory(string moduleDirectory, FunctionVisibility visibility) =>
            Path.Combine(moduleDirectory, visibility == FunctionVisibility.Public ? "Public" : "Private");

        public IEnumerable<Module> GetModules()
        {
            var storagePath = GetStoragePath();

            return Directory
                .GetDirectories(storagePath)
                .Select(d => GetModule(Path.GetFileName(d)))
                .Where(m => m != null)
                .OrderBy(m => m.Name);
        }

        public Module GetModule(string name)
        {
            var moduleDirectory = GetModuleDirectory(name);

            if (!Directory.Exists(moduleDirectory))
                return null;

            var manifestPath = GetManifestPath(moduleDirectory, name);

            var module = new Module
            {
                Name = name,
                Manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : string.Empty,
                Functions = new List<ModuleFunction>(),
            };

            foreach (var visibility in new[] { FunctionVisibility.Public, FunctionVisibility.Private })
            {
                var directory = GetVisibilityDirectory(moduleDirectory, visibility);

                if (!Directory.Exists(directory))
                    continue;

                foreach (var file in Directory.GetFiles(directory, "*.ps1").OrderBy(f => f))
                {
                    module.Functions.Add(new ModuleFunction
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Visibility = visibility,
                        Content = File.ReadAllText(file),
                    });
                }
            }

            return module;
        }

        public bool ModuleExists(string name) => Directory.Exists(GetModuleDirectory(name));

        public Module CreateScaffold(string name)
        {
            return new Module
            {
                Name = name,
                Manifest = GetDefaultManifest(name),
                Functions = new List<ModuleFunction>
                {
                    new()
                    {
                        Name = "Get-Example",
                        Visibility = FunctionVisibility.Public,
                        Content = GetDefaultFunctionContent("Get-Example"),
                    },
                },
            };
        }

        public void SaveModule(Module module)
        {
            var moduleDirectory = GetModuleDirectory(module.Name);

            if (!Directory.Exists(moduleDirectory))
                Directory.CreateDirectory(moduleDirectory);

            File.WriteAllText(GetManifestPath(moduleDirectory, module.Name), module.Manifest ?? string.Empty);
            File.WriteAllText(GetLoaderPath(moduleDirectory, module.Name), GetLoaderScript());

            var desiredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var visibility in new[] { FunctionVisibility.Public, FunctionVisibility.Private })
            {
                var directory = GetVisibilityDirectory(moduleDirectory, visibility);

                Directory.CreateDirectory(directory);

                foreach (var function in module.Functions.Where(f => f.Visibility == visibility))
                {
                    if (string.IsNullOrWhiteSpace(function.Name))
                        continue;

                    var path = Path.Combine(directory, $"{function.Name}.ps1");

                    File.WriteAllText(path, function.Content ?? string.Empty);
                    desiredFiles.Add(path);
                }
            }

            // Prune function files that were removed or moved between Public/Private
            foreach (var visibility in new[] { FunctionVisibility.Public, FunctionVisibility.Private })
            {
                var directory = GetVisibilityDirectory(moduleDirectory, visibility);

                if (!Directory.Exists(directory))
                    continue;

                foreach (var file in Directory.GetFiles(directory, "*.ps1"))
                {
                    if (!desiredFiles.Contains(file))
                        File.Delete(file);
                }
            }
        }

        public void RenameModule(string oldName, string newName)
        {
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return;

            var module = GetModule(oldName);

            if (module == null)
                return;

            DeleteModule(oldName);

            module.Name = newName;

            SaveModule(module);
        }

        public void DeleteModule(string name)
        {
            var moduleDirectory = GetModuleDirectory(name);

            if (Directory.Exists(moduleDirectory))
                Directory.Delete(moduleDirectory, true);
        }

        public string GetModulesArchive()
        {
            var storagePath = GetStoragePath();
            var archivePath = Path.Combine(Path.GetTempPath(), $"LANCommander.Modules.{Guid.NewGuid()}.zip");

            ZipFile.CreateFromDirectory(storagePath, archivePath, CompressionLevel.Optimal, false);

            return archivePath;
        }

        public ModuleManifest ParseManifest(string manifest)
        {
            var result = new ModuleManifest();

            if (string.IsNullOrWhiteSpace(manifest))
                return result;

            var ast = Parser.ParseInput(manifest, out _, out _);

            var hashtable = ast
                .Find(a => a is HashtableAst, false) as HashtableAst;

            if (hashtable == null)
                return result;

            foreach (var pair in hashtable.KeyValuePairs)
            {
                if (pair.Item1 is not StringConstantExpressionAst keyAst)
                    continue;

                var value = GetScalarString(pair.Item2);

                if (value == null)
                    continue;

                switch (keyAst.Value)
                {
                    case "RootModule":
                        result.RootModule = value;
                        break;
                    case "ModuleVersion":
                        result.ModuleVersion = value;
                        break;
                    case "GUID":
                        result.Guid = value;
                        break;
                    case "Author":
                        result.Author = value;
                        break;
                    case "CompanyName":
                        result.CompanyName = value;
                        break;
                    case "Copyright":
                        result.Copyright = value;
                        break;
                    case "Description":
                        result.Description = value;
                        break;
                    case "PowerShellVersion":
                        result.PowerShellVersion = value;
                        break;
                }
            }

            return result;
        }

        public string GenerateManifest(ModuleManifest manifest, IEnumerable<string> functionNames, IEnumerable<string> aliasNames)
        {
            var functions = functionNames?.Distinct().OrderBy(n => n).ToList() ?? new List<string>();
            var aliases = aliasNames?.Distinct().OrderBy(n => n).ToList() ?? new List<string>();

            var builder = new System.Text.StringBuilder();

            builder.Append("@{\n");
            builder.Append($"    RootModule = '{Escape(manifest.RootModule)}'\n");
            builder.Append($"    ModuleVersion = '{Escape(manifest.ModuleVersion)}'\n");
            builder.Append($"    GUID = '{Escape(manifest.Guid)}'\n");

            if (!string.IsNullOrWhiteSpace(manifest.Author))
                builder.Append($"    Author = '{Escape(manifest.Author)}'\n");

            if (!string.IsNullOrWhiteSpace(manifest.CompanyName))
                builder.Append($"    CompanyName = '{Escape(manifest.CompanyName)}'\n");

            if (!string.IsNullOrWhiteSpace(manifest.Copyright))
                builder.Append($"    Copyright = '{Escape(manifest.Copyright)}'\n");

            if (!string.IsNullOrWhiteSpace(manifest.Description))
                builder.Append($"    Description = '{Escape(manifest.Description)}'\n");

            if (!string.IsNullOrWhiteSpace(manifest.PowerShellVersion))
                builder.Append($"    PowerShellVersion = '{Escape(manifest.PowerShellVersion)}'\n");

            builder.Append($"    FunctionsToExport = {FormatArray(functions)}\n");
            builder.Append("    CmdletsToExport = @()\n");
            builder.Append("    VariablesToExport = @()\n");
            builder.Append($"    AliasesToExport = {FormatArray(aliases)}\n");
            builder.Append("}\n");

            return builder.ToString();
        }

        public IReadOnlyList<string> GetExportedFunctions(Module module)
        {
            if (module?.Functions == null)
                return [];

            return module.Functions
                .Where(f => f.Visibility == FunctionVisibility.Public && !string.IsNullOrWhiteSpace(f.Name))
                .Select(f => f.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public IReadOnlyList<string> GetExportedAliases(Module module)
        {
            if (module?.Functions == null)
                return [];

            var aliases = new List<string>();

            foreach (var function in module.Functions)
                aliases.AddRange(GetAliasesFromScript(function.Content));

            return aliases.Distinct().OrderBy(a => a).ToList();
        }

        public IEnumerable<(string Name, string Synopsis, string Module)> GetPublicFunctionCompletions()
        {
            foreach (var module in GetModules())
            {
                foreach (var function in module.Functions.Where(f =>
                             f.Visibility == FunctionVisibility.Public && !string.IsNullOrWhiteSpace(f.Name)))
                {
                    yield return (function.Name, ExtractSynopsis(function.Content), module.Name);
                }
            }
        }

        private static string ExtractSynopsis(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            var match = Regex.Match(content, @"\.SYNOPSIS\s*\r?\n\s*(?<synopsis>.+)");

            return match.Success ? match.Groups["synopsis"].Value.Trim() : null;
        }

        public IReadOnlyList<VerbGroup> GetApprovedVerbGroups() =>
            ApprovedVerbGroups
                .Select(g => new VerbGroup(g.Name, g.Verbs.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray()))
                .ToList();

        public bool IsValidFunctionName(string name) =>
            !string.IsNullOrWhiteSpace(name) && FunctionNamePattern.IsMatch(name);

        public bool IsApprovedVerb(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var dash = name.IndexOf('-');

            if (dash <= 0)
                return false;

            return ApprovedVerbs.Contains(name.Substring(0, dash));
        }

        private static IEnumerable<string> GetAliasesFromScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                yield break;

            var ast = Parser.ParseInput(script, out _, out _);

            foreach (var command in ast.FindAll(a => a is CommandAst, true).Cast<CommandAst>())
            {
                var name = command.GetCommandName();

                if (!string.Equals(name, "Set-Alias", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "New-Alias", StringComparison.OrdinalIgnoreCase))
                    continue;

                var elements = command.CommandElements;

                for (var i = 1; i < elements.Count; i++)
                {
                    if (elements[i] is CommandParameterAst parameter &&
                        string.Equals(parameter.ParameterName, "Name", StringComparison.OrdinalIgnoreCase) &&
                        i + 1 < elements.Count)
                    {
                        var value = GetScalarString(elements[i + 1]);

                        if (value != null)
                            yield return value;

                        break;
                    }

                    if (elements[i] is StringConstantExpressionAst positional && i == 1)
                    {
                        yield return positional.Value;
                        break;
                    }
                }
            }
        }

        private static string GetScalarString(Ast valueAst)
        {
            return valueAst switch
            {
                StringConstantExpressionAst s => s.Value,
                ExpandableStringExpressionAst e => e.Value,
                ConstantExpressionAst c => c.Value?.ToString(),
                _ => null,
            };
        }

        private static string FormatArray(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return "@()";

            return "@(" + string.Join(", ", values.Select(v => $"'{Escape(v)}'")) + ")";
        }

        private static string Escape(string value) =>
            (value ?? string.Empty).Replace("'", "''");

        private static string GetDefaultManifest(string name) =>
            $"@{{\n    RootModule = '{name}.psm1'\n    ModuleVersion = '1.0.0'\n    GUID = '{Guid.NewGuid()}'\n    FunctionsToExport = '*'\n}}\n";

        private static string GetDefaultFunctionContent(string name) =>
            $"function {name} {{\n    [CmdletBinding()]\n    param()\n\n    \"Hello from a LANCommander module\"\n}}\n";

        // Loader that dot-sources every function file and exports only the public ones.
        private static string GetLoaderScript() =>
            "$Public  = @(Get-ChildItem -Path \"$PSScriptRoot\\Public\\*.ps1\" -ErrorAction SilentlyContinue)\n" +
            "$Private = @(Get-ChildItem -Path \"$PSScriptRoot\\Private\\*.ps1\" -ErrorAction SilentlyContinue)\n" +
            "\n" +
            "foreach ($file in @($Public + $Private)) {\n" +
            "    try { . $file.FullName }\n" +
            "    catch { Write-Error \"Failed to import function $($file.FullName): $_\" }\n" +
            "}\n" +
            "\n" +
            "Export-ModuleMember -Function $Public.BaseName\n";
    }
}
