using LANCommander.SDK.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models
{
    public abstract class BaseModel : IKeyedModel
    {
        [Key]
        public Guid Id { get; set; }

        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; }

        [Display(Name = "Updated On")]
        public DateTime UpdatedOn { get; set; }
    }
}
