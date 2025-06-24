using Microsoft.AspNetCore.Identity;

namespace LANCommander.Server.Services.Exceptions;

public class UserRegistrationException : IdentityException
{
    public UserRegistrationException(string message) : base(message) { }
    public UserRegistrationException(IdentityResult identityResult, string message) : base(identityResult, message) { }

    public UserRegistrationException(string message, Exception? innerException) : base(message, innerException) { }
    public UserRegistrationException(IdentityResult identityResult, string message, Exception? innerException) : base(identityResult, message, innerException) { }
}
