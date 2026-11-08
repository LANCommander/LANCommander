using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>Whether a match attachment's file has been received.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchAttachmentStatus
    {
        /// <summary>The upload was started but hasn't been completed.</summary>
        Pending,

        /// <summary>The file was received, and its size and hash matched.</summary>
        Uploaded,

        /// <summary>The file didn't match the size or hash it was declared with.</summary>
        Failed,
    }
}
