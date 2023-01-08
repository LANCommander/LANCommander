using LANCommander.SDK.Models;
using RestSharp;
using RestSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    internal class LANCommanderClient
    {
        public readonly RestClient Client;
        public AuthToken Token;

        public LANCommanderClient(string baseUrl)
        {
            Client = new RestClient(baseUrl);
        }

        private T PostRequest<T>(string route, object body)
        {
            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = Client.Post<T>(request);

            return response.Data;
        }

        private T GetRequest<T>(string route)
        {
            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = Client.Get<T>(request);

            return response.Data;
        }

        private string DownloadRequest(string route, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            var client = new WebClient();
            var tempFile = Path.GetTempFileName();

            client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");
            client.DownloadProgressChanged += (s, e) => progressHandler(e);
            client.DownloadFileCompleted += (s, e) => completeHandler(e);

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            client.DownloadFileAsync(new Uri($"{Client.BaseUrl}{route}"), tempFile);

            return tempFile;
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

            if (response.StatusCode != HttpStatusCode.OK)
                throw new WebException(response.ErrorMessage);

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
            return GetRequest<IEnumerable<Game>>("/api/Games");
        }

        public Game GetGame(Guid id)
        {
            return GetRequest<Game>($"/api/Games/{id}");
        }

        public string DownloadArchive(Guid id, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            return DownloadRequest($"api/Archives/Download/{id}", progressHandler, completeHandler);
        }
    }
}
