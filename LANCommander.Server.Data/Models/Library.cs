using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Data.Models
{
    [Table("Libraries")]
    public class Library : BaseModel
    {
        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
        public virtual ICollection<Game> Games { get; set; }
    }
}
