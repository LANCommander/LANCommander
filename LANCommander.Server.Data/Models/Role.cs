using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Roles")]
    public class Role : IdentityRole<Guid>, IBaseModel
    {
        public ICollection<Collection> Collections { get; set; }
        public ICollection<UserRole> UserRoles { get; set; }
        [NotMapped]
        public ICollection<User> Users 
        {
            get
            {
                return UserRoles?.Select(ur => ur.User).ToArray() ?? [];
            }
        }

        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        public Guid? CreatedById { get; set; }
        [ForeignKey(nameof(CreatedById))]

        [Display(Name = "Created By")]
        public User? CreatedBy { get; set; }

        [Display(Name = "Updated On")]
        public DateTime UpdatedOn { get; set; }

        public Guid? UpdatedById { get; set; }
        [ForeignKey(nameof(UpdatedById))]

        [Display(Name = "Updated By")]
        public User? UpdatedBy { get; set; }
    }
}
