using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Roles")]
    public class Role : IdentityRole<Guid>, IBaseModel
    {
        public virtual ICollection<Collection> Collections { get; set; }
        [NotMapped]
        public virtual ICollection<User> Users { get; set; }

        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        public Guid? CreatedById { get; set; }
        [ForeignKey(nameof(CreatedById))]

        [Display(Name = "Created By")]
        public virtual User? CreatedBy { get; set; }

        [Display(Name = "Updated On")]
        public DateTime UpdatedOn { get; set; }

        public Guid? UpdatedById { get; set; }
        [ForeignKey(nameof(UpdatedById))]

        [Display(Name = "Updated By")]
        public virtual User? UpdatedBy { get; set; }
    }
}
