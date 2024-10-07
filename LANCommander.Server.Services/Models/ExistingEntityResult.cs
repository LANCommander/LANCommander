using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Models
{
    public class ExistingEntityResult<T> where T : BaseModel
    {
        public T Value { get; set; }
        public bool Existing { get; set; }
    }
}
