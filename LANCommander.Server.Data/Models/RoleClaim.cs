using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("UserClaims")]
    public class RoleClaim : IdentityRoleClaim<Guid> { }
}
