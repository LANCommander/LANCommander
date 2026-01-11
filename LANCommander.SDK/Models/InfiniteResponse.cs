using System.Collections.Generic;

namespace LANCommander.SDK.Models;

public class InfiniteResponse<T>
{
    public IEnumerable<T> Items { get; set; }
    public bool HasMore { get; set; }
}