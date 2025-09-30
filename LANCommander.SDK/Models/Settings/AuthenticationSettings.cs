using System;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Models;

public class AuthenticationSettings
{
    [YamlMember(typeof(string))]
    public Uri ServerAddress { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public bool OfflineModeEnabled { get; set; }
}