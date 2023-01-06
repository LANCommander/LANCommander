using LANCommander.SDK.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Playnite.Extension
{
    internal class LANCommanderClient
    {
        private readonly RestClient Client;
        private AuthToken Token;

        public LANCommanderClient()
        {
            Client = new RestClient("https://localhost:7087");
        }

        private T PostRequest<T>(string route, object body)
        {
            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = Client.Post<T>(request);

            return response.Data;
        }

        public AuthResponse Authenticate(string username, string password)
        {
            var response = Client.Post<AuthResponse>(new RestRequest("/api/Auth").AddJsonBody(new AuthRequest()
            {
                UserName = username,
                Password = password
            }));

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return response.Data;

                case HttpStatusCode.Forbidden:
                case HttpStatusCode.BadRequest:
                    throw new WebException("Invalid username or password");

                default:
                    throw new WebException("Could not communicate with the server");
            }
        }

        public AuthResponse RefreshToken(AuthToken token)
        {
            var request = new RestRequest("/api/Auth/Refresh")
                .AddJsonBody(token);

            var response = Client.Post<AuthResponse>(request);

            return response.Data;
        }

        public bool ValidateToken(AuthToken token)
        {
            var request = new RestRequest("/api/Auth/Validate")
                .AddHeader("Authorization", $"Bearer {token.AccessToken}");

            var response = Client.Post(request);

            return response.StatusCode == HttpStatusCode.OK;
        }

        public IEnumerable<Game> GetGames()
        {
            var response = Client.Get<IEnumerable<Game>>(new RestRequest("/api/Games"));

            return response.Data;
        }
    }
}
