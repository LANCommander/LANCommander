using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Roles")]
    public class Role : IdentityRole<Guid>
    {
        public virtual ICollection<Collection> Collections { get; set; }
    }
}
