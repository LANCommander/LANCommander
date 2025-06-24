using LANCommander.SDK.Models;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace LANCommander.SDK.Exceptions
{
    public class AuthFailedException : Exception
    {
        public enum AuthenticationErrorCode
        {
            InvalidCredentials,
            PasswordExpired,
            TokenExpired,
        }

        /// <summary>
        /// Categorizes the failure so callers can switch on it.
        /// </summary>
        public AuthenticationErrorCode ErrorCode { get; }

        public ErrorResponse ErrorData { get; }

        public AuthFailedException(string message) : base(message) { }

        public AuthFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public AuthFailedException(AuthenticationErrorCode errorCode, ErrorResponse errorData = null)
            : base($"Authentication failed: {errorCode}")
        {
            ErrorCode = errorCode;
            ErrorData = errorData;
        }

        public AuthFailedException(AuthenticationErrorCode errorCode, string message, ErrorResponse errorData = null, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            ErrorData = errorData;
        }
    }
}
