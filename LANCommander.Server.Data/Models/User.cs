using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Users")]
    public class User : IdentityUser<Guid>, IBaseModel
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
        public ICollection<GameSave>? GameSaves { get; set; }

        [JsonIgnore]
        public ICollection<PlaySession>? PlaySessions { get; set; }

        [JsonIgnore]
        public ICollection<Media>? Media { get; set; }

        public ICollection<UserRole> UserRoles { get; set; }
        
        [NotMapped]
        public ICollection<Role>? Roles {
            get
            {
                return UserRoles?.Select(x => x.Role).ToList();
            }
        }
        
        public ICollection<UserCustomField>? CustomFields { get; set; }

        [JsonIgnore]
        public bool Approved { get; set; }

        [JsonIgnore]
        public DateTime? ApprovedOn { get; set; }

        public string? Alias { get; set; }

        [JsonIgnore]
        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        [JsonIgnore]
        public Guid? CreatedById { get; set; }
        [ForeignKey(nameof(CreatedById))]

        [JsonIgnore]
        [Display(Name = "Created By")]
        public User? CreatedBy { get; set; }

        [JsonIgnore]
        [Display(Name = "Updated On")]
        public DateTime UpdatedOn { get; set; }

        [JsonIgnore]
        public Guid? UpdatedById { get; set; }
        [ForeignKey(nameof(UpdatedById))]

        [JsonIgnore]
        [Display(Name = "Updated By")]
        public User? UpdatedBy { get; set; }

        public Library Library { get; set; }
    }
}
