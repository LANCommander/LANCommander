using System;

namespace PCGamingWikiClient
{
    private HttpClient Client;

    public PCGamingWikiService()
    {
        Client = new HttpClient();
        Client.BaseAddress = new Uri("https://www.pcgamingwiki.com/");
    }

    public SearchResult Search(string keyword)
    {

    }
}
