namespace LANCommander.Server.Plugins.Records;

/// <summary>The checks the record providers make before they write.</summary>
internal static class RecordGuard
{
    public static KeyNotFoundException NotFound(string what, Guid id) =>
        new($"There is no {what} with the id {id}.");

    public static T Found<T>(T? record, string what, Guid id) where T : class =>
        record ?? throw NotFound(what, id);

    /// <summary>Throws unless every id was found.</summary>
    public static void AllFound(IReadOnlyCollection<Guid> ids, IEnumerable<Guid> found, string what)
    {
        var missing = ids.Except(found).ToList();

        if (missing.Count > 0)
            throw new KeyNotFoundException($"There is no {what} with the id {string.Join(", ", missing)}.");
    }

    /// <summary>Throws unless the owner is one of the kinds a record can belong to.</summary>
    public static void OwnerIs(RecordOwner owner, string records, params RecordOwnerKind[] kinds)
    {
        if (!kinds.Contains(owner.Kind))
            throw new ArgumentException(
                $"{records} belong to {string.Join(" or ", kinds.Select(kind => kind.ToString().ToLowerInvariant() + "s"))}, not {owner.Kind.ToString().ToLowerInvariant()}s.",
                nameof(owner));
    }
}
