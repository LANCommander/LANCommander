using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Launcher.Data.Models
{
    [Table("Tags")]
    public class Tag : BaseTaxonomyModel
    {
    }
}
