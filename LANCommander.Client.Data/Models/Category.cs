using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Client.Data.Models
{
    [Table("Categories")]
    public class Category : BaseTaxonomyModel
    {
        public virtual Category Parent { get; set; }
        public virtual ICollection<Category> Children { get; set; }
    }
}
