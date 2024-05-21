using LANCommander.Client.Data.Models;

namespace LANCommander.Client.Models
{
    public class ExistingEntityResult<T> where T : BaseModel
    {
        public T Value { get; set; }
        public bool Existing { get; set; }
    }
}
