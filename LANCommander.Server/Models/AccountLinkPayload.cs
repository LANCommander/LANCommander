namespace LANCommander.Server.Models;

public class AccountLinkPayload
{
    public Guid Code { get; set; }
    public Guid UserId { get; set; }
    public string AuthenticationProviderSlug { get; set; }
}