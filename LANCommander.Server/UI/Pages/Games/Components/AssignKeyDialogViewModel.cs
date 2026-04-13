using LANCommander.SDK.Enums;

namespace LANCommander.Server.UI.Pages.Games.Components;

public class AssignKeyDialogViewModel
{
    public KeyAllocationMethod KeyAllocationMethod { get; set; }
    public string MacAddress { get; set; }
    public Guid SelectedUserId { get; set; }
}