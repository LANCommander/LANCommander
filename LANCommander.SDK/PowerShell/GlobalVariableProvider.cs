using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.PowerShell
{
    /// <summary>
    /// Exposes the administrator-defined global variables as the read-only <c>var:</c> drive, so scripts
    /// read them the way they read environment variables: <c>$var:LicenseServer</c>,
    /// <c>Get-ChildItem var:</c>, <c>Test-Path var:LicenseServer</c>. A name with no variable reads as
    /// <c>$null</c>, as <c>$env:</c> does.
    /// </summary>
    [CmdletProvider(ProviderName, ProviderCapabilities.None)]
    public sealed class GlobalVariableProvider : ContainerCmdletProvider, IContentCmdletProvider
    {
        public const string ProviderName = "LANCommanderVariable";
        public const string DriveName = "var";

        /// <summary>The session variable <see cref="PowerShellRunspaceBuilder"/> puts the values in for this provider.</summary>
        public const string SessionVariable = "__LCGlobalVariables";

        private static readonly IReadOnlyDictionary<string, GlobalVariable> None =
            new Dictionary<string, GlobalVariable>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The values a runspace's <c>var:</c> drive serves, keyed case-insensitively by name.</summary>
        public static IReadOnlyDictionary<string, GlobalVariable> ToSessionValue(IEnumerable<GlobalVariable> variables) =>
            (variables ?? [])
                .Where(v => !String.IsNullOrWhiteSpace(v?.Name))
                .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        private IReadOnlyDictionary<string, GlobalVariable> Variables =>
            SessionState.PSVariable.GetValue(SessionVariable) as IReadOnlyDictionary<string, GlobalVariable> ?? None;

        private static string NameOf(string path) =>
            (path ?? String.Empty).Trim('\\', '/');

        private static bool IsRoot(string path) => NameOf(path).Length == 0;

        private bool TryGet(string path, out GlobalVariable variable) =>
            Variables.TryGetValue(NameOf(path), out variable);

        protected override Collection<PSDriveInfo> InitializeDefaultDrives() =>
        [
            new PSDriveInfo(DriveName, ProviderInfo, String.Empty, "LANCommander global script variables", null),
        ];

        protected override bool IsValidPath(string path) => true;

        protected override bool ItemExists(string path) => IsRoot(path) || TryGet(path, out _);

        protected override bool HasChildItems(string path) => IsRoot(path) && Variables.Count > 0;

        protected override void GetItem(string path)
        {
            if (IsRoot(path))
            {
                WriteItemObject(PSDriveInfo, path, true);
                return;
            }

            if (TryGet(path, out var variable))
                WriteItemObject(ToEntry(variable), variable.Name, false);
        }

        protected override void GetChildItems(string path, bool recurse)
        {
            if (!IsRoot(path))
            {
                GetItem(path);
                return;
            }

            foreach (var variable in Variables.Values.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
                WriteItemObject(ToEntry(variable), variable.Name, false);
        }

        protected override void GetChildNames(string path, ReturnContainers returnContainers)
        {
            if (!IsRoot(path))
                return;

            foreach (var name in Variables.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                WriteItemObject(name, name, false);
        }

        // Like Env:, items are name/value pairs, so Get-ChildItem var: lists as a Name/Value table
        private static DictionaryEntry ToEntry(GlobalVariable variable) => new(variable.Name, variable.Value ?? String.Empty);

        public IContentReader GetContentReader(string path) =>
            new ValueReader(TryGet(path, out var variable) ? variable.Value ?? String.Empty : null);

        public object GetContentReaderDynamicParameters(string path) => null;

        public IContentWriter GetContentWriter(string path) => throw ReadOnly(path);

        public object GetContentWriterDynamicParameters(string path) => null;

        public void ClearContent(string path) => throw ReadOnly(path);

        public object ClearContentDynamicParameters(string path) => null;

        private static PSNotSupportedException ReadOnly(string path) =>
            new($"var:{NameOf(path)} is read-only. Global variables are set on the server under Scripting > Variables.");

        /// <summary>Reads one value, once; a missing variable reads as nothing.</summary>
        private sealed class ValueReader(string value) : IContentReader
        {
            private bool _read;

            public IList Read(long readCount)
            {
                if (_read || value == null)
                    return new List<object>();

                _read = true;

                return new List<object> { value };
            }

            public void Seek(long offset, System.IO.SeekOrigin origin) => _read = offset > 0;

            public void Close() { }

            public void Dispose() { }
        }
    }
}
