using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data.Models
{
    /// <summary>
    /// A long-lived bearer credential a user issues to a script, tool or MCP client. Only a hash of
    /// the token is stored; the plaintext is shown once, when the token is created.
    /// </summary>
    [Table("PersonalAccessTokens")]
    [Index(nameof(TokenHash), IsUnique = true)]
    public class PersonalAccessToken : BaseModel
    {
        [MaxLength(128)]
        public string Name { get; set; } = "";

        /// <summary>Hex-encoded SHA-256 of the full token.</summary>
        [JsonIgnore]
        [MaxLength(64)]
        public string TokenHash { get; set; } = "";

        /// <summary>The first characters of the token, so its owner can tell tokens apart.</summary>
        [MaxLength(32)]
        public string TokenPrefix { get; set; } = "";

        /// <summary>Space-separated scopes, e.g. <c>api</c> or <c>plugin:Mcp</c>.</summary>
        [MaxLength(1024)]
        public string Scopes { get; set; } = "";

        public DateTime? ExpiresOn { get; set; }

        public DateTime? LastUsedOn { get; set; }

        public Guid UserId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(UserId))]
        [InverseProperty("PersonalAccessTokens")]
        public User? User { get; set; }
    }
}
