using System;

namespace LANCommander.SDK.Models;

public class AuthenticationSettings
{
    public Uri ServerAddress { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public bool OfflineModeEnabled { get; set; }
}