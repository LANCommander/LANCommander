using LANCommander.Server.Services;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    [Table("GameSaves")]
    public class GameSave : BaseModel
    {
        public Guid? GameId { get; set; }
        [JsonIgnore]
        [ForeignKey(nameof(GameId))]
        [InverseProperty("GameSaves")]
        public virtual Game? Game { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        [InverseProperty("GameSaves")]
        public virtual User? User { get; set; }

        public string GetUploadPath()
        {
            var settings = SettingService.GetSettings();

            return Path.Combine(settings.UserSaves.StoragePath, UserId.ToString(), GameId.ToString(), Id.ToString());
        }
    }
}
