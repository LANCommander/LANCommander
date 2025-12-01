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

    public static IEnumerable<Uri> SuggestValidUris(this string url)
    {
        List<Uri> uris = [];

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
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
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