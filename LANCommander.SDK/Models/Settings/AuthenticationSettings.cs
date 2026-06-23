using System;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Models;

public class AuthenticationSettings
{
    [YamlMember(typeof(string))]
    public Uri ServerAddress { get; set; }
    public AuthToken Token { get; set; }
    public bool OfflineModeEnabled { get; set; }
    public bool AllowRegistration { get; set; } = true;
    public bool AutoRedirectToProvider { get; set; } = false;
}