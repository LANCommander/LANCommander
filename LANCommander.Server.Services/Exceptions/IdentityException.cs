using Microsoft.AspNetCore.Identity;

namespace LANCommander.Server.Services.Exceptions;

public abstract class IdentityException : Exception
{
    public IdentityResult IdentityResult { get; set; }

    public IdentityException(string message) : this(message, innerException: null)
    {
    }

    public IdentityException(string message, Exception? innerException) : base(message, innerException)
    {
        IdentityResult = IdentityResult.Failed(new IdentityError()
        {
            Code = "",
            Description = message,
        });
    }

    public IdentityException(IdentityResult identityResult, string message) : this(identityResult, message, innerException: null)
    { 
    }

    public IdentityException(IdentityResult identityResult, string message, Exception? innerException) : base(message)
    {
        IdentityResult = identityResult;
    }
}