using LANCommander.SDK.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    public abstract class BaseModel : IBaseModel
    {
        [Key]
        public Guid Id { get; set; }

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
