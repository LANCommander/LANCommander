using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("PlaySessions")]
    public class PlaySession : BaseModel
    {
        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("PlaySessions")]
        public Game? Game { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        [InverseProperty("PlaySessions")]
        public User? User { get; set; }

        /// <summary>
        /// The game server the player joined, when the launcher knows it: picked by a connect action, or
        /// detected from the game's network traffic.
        /// </summary>
        public Guid? ServerId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(ServerId))]
        [InverseProperty(nameof(Models.Server.PlaySessions))]
        public Server? Server { get; set; }

        /// <summary>The name the player played under, as the launcher set it for the game.</summary>
        [MaxLength(255)]
        public string? Alias { get; set; }

        [Display(Name = "Start")]
        public DateTime? Start { get; set; }

        [Display(Name = "End")]
        public DateTime? End { get; set; }

        public TimeSpan? Duration
        {
            get
            {
                if (!Start.HasValue) return null;
                if (!End.HasValue) return null;

                return End.Value.Subtract(Start.Value);
            }
        }
    }
}
