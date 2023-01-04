using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Roles")]
    public class Role : IdentityRole<Guid>
    {
    }
}
