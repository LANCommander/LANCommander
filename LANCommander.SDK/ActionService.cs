using LANCommander.SDK.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Text;

namespace LANCommander.SDK
{
    internal class ActionVariables
    {
        private readonly Client Client;

        public ActionVariables(Client client) {
            Client = client;
        }

        public string ServerAddress
        {
            get
            {
                return Client.GetServerAddress();
            }
        }

        public string IPXRelayHost
        {
            get
            {
                var host = Client.Settings.IPXRelayHost;

                if (String.IsNullOrWhiteSpace(host))
                {
                    var serverAddress = new Uri(Client.GetServerAddress());

                    host = serverAddress.DnsSafeHost;
                }

                var entry = Dns.GetHostEntry(host);

                if (entry.AddressList.Length > 0)
                    host = entry.AddressList.First().ToString();

                return host;
            }
        }

        public int IPXRelayPort
        {
            get
            {
                return Client.Settings.IPXRelayPort;
            }
        }
    }

    public class ActionService
    {
        private readonly Client Client;
        private readonly ActionVariables ActionVariables;

        private Dictionary<string, string> CustomVariables { get; set; }

        public ActionService(Client client)
        {
            Client = client;
            ActionVariables = new ActionVariables(Client);
            CustomVariables = new Dictionary<string, string>();
        }

        public void AddVariable(string key, string value)
        {
            CustomVariables[key] = value;
        }

        public string ExpandVariables(string input, string installDirectory, Dictionary<string, string> additionalVariables = null)
        {
            if (input == null)
                return input;

            var variables = typeof(ActionVariables).GetProperties();

            foreach (var variable in variables)
            {
                if (input.Contains($"{{{variable.Name}}}"))
                    input = input.Replace($"{{{variable.Name}}}", $"{variable.GetValue(ActionVariables)}");
            }

            foreach (var variable in CustomVariables)
            {
                input = input.Replace($"{{{variable.Key}}}", variable.Value);
            }

            if (additionalVariables != null)
            foreach (var variable in additionalVariables)
            {
                input = input.Replace($"{{{variable.Key}}}", variable.Value);
            }

            return input.ExpandEnvironmentVariables(installDirectory);
        }
    }
}
