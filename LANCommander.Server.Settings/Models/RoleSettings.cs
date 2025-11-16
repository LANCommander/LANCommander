namespace LANCommander.Server.Settings.Models;

public class RoleSettings
{
    public Guid DefaultRoleId { get; set; }
    public bool RestrictGamesByCollection { get; set; } = false;
}