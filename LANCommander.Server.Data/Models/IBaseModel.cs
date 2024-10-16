using LANCommander.SDK.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Data.Models
{
    public interface IBaseModel : IKeyedModel
    {
        Guid Id { get; set; }
        DateTime CreatedOn { get; set; }
        User? CreatedBy { get; set; }
        DateTime UpdatedOn { get; set; }
        User? UpdatedBy { get; set; }
    }
}
