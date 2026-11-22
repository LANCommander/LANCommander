using System;
using System.Net;

namespace LANCommander.SDK.Exceptions
{
    /// <summary>The server refused a change to the player's profile. The message is fit to show the player.</summary>
    public class ProfileUpdateException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public ProfileUpdateException(string message, HttpStatusCode statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
