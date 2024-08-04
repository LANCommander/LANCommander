using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("Genres")]
    public class Genre : BaseTaxonomyModel
    {
    }
}
