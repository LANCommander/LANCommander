using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Launcher.Data.Models
{
    [Table("Genres")]
    public class Genre : BaseTaxonomyModel
    {
    }
}
