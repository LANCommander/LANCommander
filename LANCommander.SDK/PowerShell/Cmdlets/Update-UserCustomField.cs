using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Cmdlets;

/// <summary>
/// Writes a custom field against the currently signed in user's profile.
/// </summary>
[Cmdlet(VerbsData.Update, "UserCustomField")]
[OutputType(typeof(string))]
public class UpdateUserCustomFieldCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the custom field to update.")]
    public string Name { get; set; }

    [Parameter(Mandatory = true, Position = 1, HelpMessage = "The new value to set for the custom field.")]
    public string Value { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profileClient = ScriptServicesProvider.GetProfileClient(SessionState);

            var result = await profileClient.UpdateCustomFieldAsync(Name, Value);

            WriteObject(result);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "UpdateUserCustomFieldError", ErrorCategory.NotSpecified, Name));
        }
    }
}
