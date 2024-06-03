using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Client.Data.Models
{
    [Table("PlaySessions")]
    public class PlaySession : BaseModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("PlaySessions")]
        public virtual Game? Game { get; set; }
        public Guid UserId { get; set; }
    }
}
