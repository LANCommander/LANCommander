using System;

namespace LANCommander.SDK.Models;

public class AuthenticationSettings : IAuthenticationSettings
{
    public Uri ServerAddress { get; set; }
    public string Token { get; set; }
    public bool OfflineModeEnabled { get; set; }
}