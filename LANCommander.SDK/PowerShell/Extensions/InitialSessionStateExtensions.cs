using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using LANCommander.SDK.PowerShell.Cmdlets;

namespace LANCommander.SDK.PowerShell.Extensions;

public static class InitialSessionStateExtensions
{
    public static void AddCustomCmdlets(this InitialSessionState initialSessionState)
    {
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Convert-AspectRatio", typeof(ConvertAspectRatioCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("ConvertFrom-SerializedBase64", typeof(ConvertFromSerializedBase64Cmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("ConvertTo-SerializedBase64", typeof(ConvertToSerializedBase64Cmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("ConvertTo-StringBytes", typeof(ConvertToStringBytesCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Edit-PatchBinary", typeof(EditPatchBinaryCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Edit-PatchGameSpy", typeof(EditPatchGameSpy), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-GameManifest", typeof(GetGameManifestCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-HorizontalFov", typeof(GetHorizontalFovCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-PrimaryDisplay", typeof(GetPrimaryDisplayCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-UserCustomField", typeof(GetUserCustomFieldCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-VerticalFov", typeof(GetVerticalFovCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Out-PlayerAvatar", typeof(OutPlayerAvatarCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Update-IniValue", typeof(UpdateIniValueCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Update-UserCustomField", typeof(UpdateUserCustomFieldCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Write-GameManifest", typeof(WriteGameManifestCmdlet), null));
        initialSessionState.Commands.Add(new SessionStateCmdletEntry("Write-ReplaceContentInFile", typeof(ReplaceContentInFileCmdlet), null));
    }
}