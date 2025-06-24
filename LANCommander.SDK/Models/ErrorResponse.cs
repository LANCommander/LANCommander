using System.Collections.Generic;
using System.Linq;

namespace LANCommander.SDK.Models;

public class ErrorResponse
{
    public struct ErrorInfo
    {
        public string Key { get; set; }
        public string Message { get; set; }

        internal static string GetMessage(ErrorInfo info) => info.Message;
    }

    /// <summary>
    /// Short error title or code.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Detailed, user-friendly error message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Optional list of detailed error messages (e.g., from validation failures).
    /// </summary>
    public IEnumerable<ErrorInfo> Details { get; set; }

    /// <summary>
    /// Optional list of detailed error messages (e.g., from validation failures).
    /// </summary>
    public IEnumerable<string> DetailsMessages => Details?.Select(ErrorInfo.GetMessage);
}
