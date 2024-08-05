using LANCommander.SDK.Exceptions;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Interceptors;
using Semver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    internal class VersionInterceptor : Interceptor
    {
        private readonly ILogger Logger;

        public override ValueTask AfterRequest(RestResponse response, CancellationToken cancellationToken)
        {
            try
            {
                var version = Client.GetCurrentVersion();
                var header = response.Headers.FirstOrDefault(h => h.Name == "X-API-Version");

                if (response.IsSuccessful && header == null)
                {
                    response.ErrorException = new ApiVersionMismatchException(version, null, $"The server is out of date and does not support client version {version}.");

                    return new ValueTask();
                }

                var apiVersion = SemVersion.Parse((string)header.Value, SemVersionStyles.Any);

                if (version.Major != apiVersion.Major || version.Minor != apiVersion.Minor)
                {
                    switch (version.ComparePrecedenceTo(apiVersion))
                    {
                        case -1:
                            response.ErrorException = new ApiVersionMismatchException(version, apiVersion, $"Your client (v{version}) is out of date and is not supported by the server (v{apiVersion})");
                            break;

                        case 1:
                            response.ErrorException = new ApiVersionMismatchException(version, apiVersion, $"Your client (v{version}) is on a version not supported by the server (v{apiVersion})");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not validate API version");
            }

            return new ValueTask();
        }
    }
}
