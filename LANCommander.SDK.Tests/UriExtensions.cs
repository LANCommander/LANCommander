using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests;

public class UriExtensions
{
    [Fact]
    public void ValidUriShouldIncludeKnownPorts()
    {
        var input = "http://localhost:5000";
        
        var uris = input.SuggestValidUris();
        var strings = uris.Select(u => u.ToString()).ToList();
        
        Assert.Equal(10, strings.Count);
        Assert.Contains("http://localhost:5000/", strings);
        Assert.Contains("https://localhost:5000/", strings);
        Assert.Contains("http://localhost/", strings);
        Assert.Contains("http://localhost:443/", strings);
        Assert.Contains("https://localhost/", strings);
        Assert.Contains("https://localhost:80/", strings);
        Assert.Contains("http://localhost:1337/", strings);
        Assert.Contains("https://localhost:1337/", strings);
        Assert.Contains("http://localhost:31337/", strings);
        Assert.Contains("https://localhost:31337/", strings);
    }

    [Fact]
    public void ValidIpAddressShouldReturnValidUris()
    {
        var input = "10.0.1.10";
        
        var uris = input.SuggestValidUris();
        var strings = uris.Select(u => u.ToString()).ToList();

        Assert.Equal(8, strings.Count);
        Assert.Contains("http://10.0.1.10:1337/", strings);
        Assert.Contains("https://10.0.1.10:1337/", strings);
        Assert.Contains("http://10.0.1.10/", strings);
        Assert.Contains("http://10.0.1.10:443/", strings);
        Assert.Contains("https://10.0.1.10/", strings);
        Assert.Contains("https://10.0.1.10:80/", strings);
        Assert.Contains("http://10.0.1.10:31337/", strings);
        Assert.Contains("https://10.0.1.10:31337/", strings);
    }
}