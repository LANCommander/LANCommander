using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Server.Data.Models
{
    [Table("Categories")]
    public class Category : BaseTaxonomyModel
    {
        public Category Parent { get; set; }
        public ICollection<Category> Children { get; set; }
    }
}
