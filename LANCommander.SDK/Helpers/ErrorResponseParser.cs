using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Helpers;

/// <summary>
/// Turns a failed HTTP response into an <see cref="ErrorResponse"/> the UI can show the user.
/// The server returns several shapes for a 400 depending on which code path failed, so try each
/// of them in turn rather than giving up and reporting a generic message.
/// </summary>
internal static class ErrorResponseParser
{
    private const int MaxRawBodyLength = 500;

    internal static async Task<ErrorResponse> ParseAsync(HttpResponseMessage response, ILogger logger = null)
    {
        if (response == null)
            return new ErrorResponse { Message = "Could not communicate with the server." };

        string body = null;

        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Could not read the error response body");
        }

        return Parse(body, (int)response.StatusCode, response.ReasonPhrase, logger);
    }

    internal static ErrorResponse Parse(string body, int statusCode, string reasonPhrase, ILogger logger = null)
    {
        if (String.IsNullOrWhiteSpace(body))
            return FromStatus(statusCode, reasonPhrase);

        // The expected shape: an ErrorResponse from one of the auth endpoints.
        try
        {
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(body, SdkJsonOptions.Default);

            // A payload of a different shape can bind successfully but leave everything null, so
            // only accept the result if something actually came through.
            if (HasContent(errorResponse))
                return errorResponse;
        }
        catch (JsonException)
        {
            // Fall through to the other shapes.
        }

        // Some endpoints return a bare JSON string for unexpected failures.
        try
        {
            var message = JsonSerializer.Deserialize<string>(body, SdkJsonOptions.Default);

            if (!String.IsNullOrWhiteSpace(message))
                return new ErrorResponse { Message = message };
        }
        catch (JsonException)
        {
            // Fall through to the raw body.
        }

        logger?.LogDebug("Could not parse the error response body as an {Type}", nameof(ErrorResponse));

        var raw = body.Trim();

        if (raw.Length > MaxRawBodyLength)
            raw = raw.Substring(0, MaxRawBodyLength) + "…";

        return String.IsNullOrWhiteSpace(raw)
            ? FromStatus(statusCode, reasonPhrase)
            : new ErrorResponse { Message = raw };
    }

    private static bool HasContent(ErrorResponse errorResponse)
    {
        if (errorResponse == null)
            return false;

        return !String.IsNullOrWhiteSpace(errorResponse.Error)
               || !String.IsNullOrWhiteSpace(errorResponse.Message)
               || (errorResponse.Details?.Any() ?? false);
    }

    private static ErrorResponse FromStatus(int statusCode, string reasonPhrase)
    {
        var message = String.IsNullOrWhiteSpace(reasonPhrase)
            ? $"The server responded with {statusCode}."
            : $"The server responded with {statusCode} {reasonPhrase}.";

        return new ErrorResponse { Message = message };
    }
}
