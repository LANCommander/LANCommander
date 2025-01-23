using Microsoft.AspNetCore.Identity;

namespace LANCommander.Server.Services.Exceptions;

public class AddRoleException : Exception
{
    public IdentityResult IdentityResult { get; set; }

    public AddRoleException(IdentityResult identityResult, string message) : base(message)
    {
        IdentityResult = identityResult;
    }
}