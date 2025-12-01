using System;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Exceptions
{
    public class RegisterFailedException : Exception
    {
        public ErrorResponse? ErrorData  { get; }

        public RegisterFailedException(string message) : base(message) { }

        public RegisterFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public RegisterFailedException(ErrorResponse errorData)
            : base($"Registration failed: {errorData}")
        {
            ErrorData = errorData;
        }

        public RegisterFailedException(string message, ErrorResponse? errorData = null, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorData = errorData;
        }
    }
}
