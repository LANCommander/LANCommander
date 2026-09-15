using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Cmdlets;

/// <summary>
/// Reads a custom field stored against the currently signed in user's profile.
/// </summary>
[Cmdlet(VerbsCommon.Get, "UserCustomField")]
[OutputType(typeof(string))]
public class GetUserCustomFieldCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the custom field to retrieve for the current user.")]
    public string Name { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profileClient = ScriptServicesProvider.GetProfileClient(SessionState);

            var result = await profileClient.GetCustomFieldAsync(Name);

            WriteObject(result);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetUserCustomFieldError", ErrorCategory.NotSpecified, Name));
        }
    }
}
