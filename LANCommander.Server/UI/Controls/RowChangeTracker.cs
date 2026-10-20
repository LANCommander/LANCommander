using LANCommander.Server.Data.Models;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// Tells whether the rows of a list editor (consoles, HTTP paths, actions) have changed since they
/// were loaded or saved: a row not saved yet counts as a change, and so does a saved row whose
/// snapshot differs. Rows removed from the list are deleted straight away, so they're forgotten.
/// </summary>
/// <param name="snapshot">The row's edited fields, as a value that compares by value (e.g. a tuple).</param>
public sealed class RowChangeTracker<T>(Func<T, object> snapshot) where T : BaseModel
{
    Dictionary<Guid, object> _saved = [];

    /// <summary>Takes the rows as they are now as saved, e.g. after loading or saving them.</summary>
    public void Reset(IEnumerable<T> rows) =>
        _saved = rows.Where(r => r.Id != Guid.Empty).ToDictionary(r => r.Id, snapshot);

    /// <summary>Forgets a row that was deleted.</summary>
    public void Forget(T row) => _saved.Remove(row.Id);

    public bool IsDirty(IEnumerable<T> rows) =>
        rows.Any(r => r.Id == Guid.Empty || !_saved.TryGetValue(r.Id, out var saved) || !Equals(saved, snapshot(r)));
}
