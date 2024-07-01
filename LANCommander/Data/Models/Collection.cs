using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    [Table("Collections")]
    public class Collection : BaseTaxonomyModel
    {
        [JsonIgnore]
        public virtual ICollection<Role> Roles { get; set; }
    }
}
