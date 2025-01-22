﻿using System.ComponentModel.DataAnnotations;
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
