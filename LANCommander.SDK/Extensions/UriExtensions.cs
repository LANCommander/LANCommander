using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace LANCommander.SDK.Extensions;

public static class UriExtensions
{
    public static IEnumerable<Uri> SuggestValidUris(this string url)
    {
        List<Uri> uris = new();

        int[] knownPorts =
        {
            1337,
            80,
            443,
            31337,
        };

        // Generate URIs based on provided URL 
        try
        {
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                var uri = new Uri(url);
            
                uris.Add(uri);
            
                if (uri.Scheme != Uri.UriSchemeHttp)
                    uris.Add(new UriBuilder(url)
                    {
                        Scheme = Uri.UriSchemeHttp,
                        Port = uri.Port,
                    }.Uri);
                else if (uri.Scheme == Uri.UriSchemeHttps)
                    uris.Add(new UriBuilder(url)
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Port = uri.Port,
                    }.Uri);
            }
            else
            {
                var basePart = url.Split("://").Last();
            
                uris.Add(new Uri($"http://{basePart}"));
                uris.Add(new Uri($"https://{basePart}"));
            }
        }
        catch { }
        
        // Additionally check known-ports
        foreach (var uri in uris.ToArray())
        {
            foreach (var port in knownPorts)
            {
                if (uri.Port != port)
                {
                    uris.Add(new UriBuilder(uri)
                    {
                        Port = port,
                    }.Uri);
                }
            }
        }

        return uris;
    }
}