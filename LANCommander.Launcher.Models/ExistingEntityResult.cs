using LANCommander.Launcher.Data.Models;

namespace LANCommander.Launcher.Models
{
    public class ExistingEntityResult<T> where T : BaseModel
    {
        public T Value { get; set; }
        public bool Existing { get; set; }
    }
}
