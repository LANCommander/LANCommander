using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Models
{
    public interface IKeyedModel
    {
        Guid Id { get; set; }
    }
}
