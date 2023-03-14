using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class AuthResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime Expiration { get; set; }
        public string Message { get; set; }
    }
}
