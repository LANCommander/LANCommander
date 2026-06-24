using System.Management.Automation;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    /// <summary>
    /// Creates a new package result object with the specified path, version, and changelog.
    /// The resulting object is set as the script's return value for the packaging pipeline.
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Package")]
    [OutputType(typeof(Package))]
    public class NewPackageCmdlet : PSCmdlet
    {
        /// <summary>
        /// Gets or sets the path to the directory containing the package contents to be archived.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true, HelpMessage = "Specifies the path to the directory containing the package contents to be archived.")]
        [ValidateNotNullOrEmpty]
        [Alias("p", "Directory")]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the version number for this package.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true, HelpMessage = "Specifies the version number for this package.")]
        [ValidateNotNullOrEmpty]
        [Alias("v")]
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the changelog describing changes in this package version.
        /// </summary>
        [Parameter(Mandatory = false, Position = 2, ValueFromPipelineByPropertyName = true, HelpMessage = "Specifies the changelog describing changes in this package version.")]
        [Alias("c", "Notes")]
        public string Changelog { get; set; }

        protected override void ProcessRecord()
        {
            var package = new Package
            {
                Path = Path,
                Version = Version,
                Changelog = Changelog
            };

            SessionState.PSVariable.Set("Return", package);

            WriteObject(package);
        }
    }
}
