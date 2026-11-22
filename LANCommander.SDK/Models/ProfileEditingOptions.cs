namespace LANCommander.SDK.Models;

/// <summary>Which parts of their profile the server lets a player change.</summary>
public class ProfileEditingOptions
{
    public bool CanChangeAlias { get; set; }
    public bool CanChangeAvatar { get; set; }
}
