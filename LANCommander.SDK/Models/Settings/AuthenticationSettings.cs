using System;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Models;

public class AuthenticationSettings
{
    [YamlMember(typeof(string))]
    public Uri ServerAddress { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.Plain)]
    public string AccessToken { get; set; }
    [YamlMember(ScalarStyle = ScalarStyle.Plain)]
    public string RefreshToken { get; set; }
    public bool OfflineModeEnabled { get; set; }
}