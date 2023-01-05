using LANCommander.SDK.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Playnite.Extension
{
    internal class LANCommanderClient
    {
        private readonly RestClient Client;

        public LANCommanderClient()
        {
            Client = new RestClient("https://localhost:7087");
        }

        public IEnumerable<Game> GetGames()
        {
            var response = Client.Get<IEnumerable<Game>>(new RestRequest("/api/Games"));

            return response.Data;
        } 
    }
}
