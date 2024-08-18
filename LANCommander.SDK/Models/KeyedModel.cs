using System;

namespace LANCommander.SDK.Models
{
    public abstract class KeyedModel : IKeyedModel
    {
        public Guid Id { get; set; }
    }
}
