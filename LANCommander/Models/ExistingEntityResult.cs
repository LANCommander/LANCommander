using LANCommander.Data.Models;

namespace LANCommander.Models
{
    public class ExistingEntityResult<T> where T : BaseModel
    {
        public T Value { get; set; }
        public bool Existing { get; set; }
    }
}
