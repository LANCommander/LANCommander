using Force.Crc32;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using RestSharp;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.Services
{
    public class TagService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public TagService(Client client)
        {
            Client = client;
        }

        public TagService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<Tag> CreateAsync(Tag tag)
        {
            return await Client.PostRequestAsync<Tag>("/api/Tags", tag);
        }

        public async Task<Tag> UpdateAsync(Tag tag)
        {
            return await Client.PostRequestAsync<Tag>($"/api/Tags/{tag.Id}", tag);
        }

        public async Task DeleteAsync(Tag tag)
        {
            await Client.DeleteRequestAsync<Tag>($"/api/Tags/{tag.Id}");
        }
    }
}
