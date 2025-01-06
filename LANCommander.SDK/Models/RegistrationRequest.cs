using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Models
{
    public class RegistrationRequest
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string PasswordConfirmation { get; set; }
    }
}
