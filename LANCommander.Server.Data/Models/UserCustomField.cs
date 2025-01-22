using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class UserCustomField : BaseModel
    {
        [MaxLength(64)]
        public string Name { get; set; }

        [MaxLength(1024)]
        public string Value { get; set; }

        public Guid? UserId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(UserId))]
        [InverseProperty("CustomFields")]
        public User? User { get; set; }
    }
}
