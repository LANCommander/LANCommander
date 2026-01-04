namespace LANCommander.UI.Components;

public class InfiniteLoadResponse<T>
{
    public T? Next { get; set; }
    public IEnumerable<T>? Items { get; set; }
    public bool HasMore { get; set; }
}