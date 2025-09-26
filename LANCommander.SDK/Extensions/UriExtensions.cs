using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace LANCommander.SDK.Extensions;

public static class UriExtensions
{
    public static Uri Join(this Uri uri, params string[] parts)
    {
        var strUri = uri.ToString().TrimEnd('/');

        foreach (var part in parts)
            strUri += '/' + part.Trim('/');
        
        return new Uri(strUri);
    }
    
    /// <summary>
    /// Creates an absolute URI from a string that may or may not include a scheme.
    /// If the string does not contain a scheme (e.g., "http://" or "https://"), it defaults to "http://".
    /// Throws an exception if the input is null, empty, or not a valid URI.
    /// </summary>
    /// <param name="input">The input URI string.</param>
    /// <returns>A valid absolute Uri.</returns>
    /// <exception cref="ArgumentException">Thrown if the input is invalid.</exception>
    public static Uri CreateUri(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("The input URI string cannot be null or empty.", nameof(input));
        }

        // Prepend default scheme if missing.
        if (!input.Contains("://"))
        {
            input = "http://" + input;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri result))
        {
            throw new ArgumentException($"The provided string '{input}' is not a valid absolute URI.", nameof(input));
        }

        return result;
    }

    /// <summary>
    /// Tries to create an absolute URI from the provided string.
    /// If no scheme is present, it defaults to "http://".
    /// </summary>
    /// <param name="input">The input URI string.</param>
    /// <param name="result">The created URI if successful; otherwise, null.</param>
    /// <returns>True if a valid URI is created; otherwise, false.</returns>
    public static bool TryCreateUri(string input, out Uri result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // Prepend default scheme if missing.
        if (!input.Contains("://"))
        {
            input = "http://" + input;
        }

        return Uri.TryCreate(input, UriKind.Absolute, out result);
    }

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
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute) && TryCreateUri(url, out var uri))
            {
                uris.Add(uri);
            
                if (uri.Scheme != Uri.UriSchemeHttp)
                    uris.Add(new UriBuilder(url)
                    {
                        Scheme = Uri.UriSchemeHttp,
                        Port = uri.Port,
                    }.Uri);
                else if (uri.Scheme != Uri.UriSchemeHttps)
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