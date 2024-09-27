using LANCommander.Server.Services;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Users")]
    public class User : IdentityUser<Guid>
    {
        // Ignore the following properties from leaking into REST requests
        [JsonIgnore]
        public override string? PasswordHash { get; set; }
        [JsonIgnore]
        public override string? SecurityStamp { get; set; }
        [JsonIgnore]
        public override string? Email { get; set; }
        [JsonIgnore]
        public override string? NormalizedEmail { get; set; }
        [JsonIgnore]
        public override bool EmailConfirmed { get; set; }
        [JsonIgnore]
        public override string? NormalizedUserName { get; set; }
        [JsonIgnore]
        public override string? ConcurrencyStamp { get; set; }
        [JsonIgnore]
        public override string? PhoneNumber { get; set; }
        [JsonIgnore]
        public override bool PhoneNumberConfirmed { get; set; }
        [JsonIgnore]
        public override bool TwoFactorEnabled { get; set; }
        [JsonIgnore]
        public override DateTimeOffset? LockoutEnd { get; set; }
        [JsonIgnore]
        public override bool LockoutEnabled { get; set; }
        [JsonIgnore]
        public override int AccessFailedCount { get; set; }

        // Refresh Token
        [JsonIgnore]
        public string? RefreshToken { get; set; }
        [JsonIgnore]
        public DateTime RefreshTokenExpiration { get; set; }

        [JsonIgnore]
        public virtual ICollection<GameSave>? GameSaves { get; set; }

        [JsonIgnore]
        public virtual ICollection<PlaySession>? PlaySessions { get; set; }

        [JsonIgnore]
        public virtual ICollection<Media>? Media { get; set; }

        public virtual ICollection<UserCustomField>? CustomFields { get; set; }

        [JsonIgnore]
        public bool Approved { get; set; }

        [JsonIgnore]
        public DateTime? ApprovedOn { get; set; }

        public string? Alias { get; set; }

        public string GetGameSaveUploadPath()
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(settings.UserSaves.StoragePath, Id.ToString());
        }
    }
}
