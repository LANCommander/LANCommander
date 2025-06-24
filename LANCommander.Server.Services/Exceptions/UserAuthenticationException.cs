using Microsoft.AspNetCore.Identity;

namespace LANCommander.Server.Services.Exceptions;

public class UserAuthenticationException : IdentityException
{
    public UserAuthenticationException(string message) : base(message) { }
    public UserAuthenticationException(IdentityResult identityResult, string message) : base(identityResult, message) { }

    public UserAuthenticationException(string message, Exception? innerException) : base(message, innerException) { }
    public UserAuthenticationException(IdentityResult identityResult, string message, Exception? innerException) : base(identityResult, message, innerException) { }

}
