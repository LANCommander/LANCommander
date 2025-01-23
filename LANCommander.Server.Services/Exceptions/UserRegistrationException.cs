using Microsoft.AspNetCore.Identity;

namespace LANCommander.Server.Services.Exceptions;

public class UserRegistrationException : Exception
{
    public IdentityResult IdentityResult { get; set; }

    public UserRegistrationException(IdentityResult identityResult, string message) : base(message)
    {
        IdentityResult = identityResult;
    }
}