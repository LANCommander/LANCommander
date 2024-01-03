using LANCommander.SDK.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

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

        public int DisplayWidth
        {
            get
            {
                return Screen.AllScreens.FirstOrDefault(s => s.Primary).Bounds.Width;
            }
        }

        public int DisplayHeight
        {
            get
            {
                return Screen.AllScreens.FirstOrDefault(s => s.Primary).Bounds.Height;
            }
        }
    }

    public class ActionService
    {
        private readonly Client Client;
        private readonly ActionVariables ActionVariables;

        public ActionService(Client client)
        {
            Client = client;
            ActionVariables = new ActionVariables(Client);
        }

        public string ExpandVariables(string input, string installDirectory)
        {
            if (input == null)
                return input;

            var variables = typeof(ActionVariables).GetProperties();

            foreach (var variable in variables)
            {
                if (input.Contains($"{{{variable.Name}}}"))
                    input = input.Replace($"{{{variable.Name}}}", $"{variable.GetValue(ActionVariables)}");
            }

            return input.ExpandEnvironmentVariables(installDirectory);
        }
    }
}
