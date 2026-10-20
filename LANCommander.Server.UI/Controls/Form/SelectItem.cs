namespace LANCommander.Server.UI.Controls;

/// <summary>
/// One choice in a <see cref="Select{TValue}"/>, optionally with a small image beside its label, or
/// in a <see cref="Segmented{TValue}"/>, optionally with an icon before its label.
/// </summary>
public sealed record SelectItem<TValue>(TValue Value, string Label, bool Disabled = false, string? Image = null, IconType Icon = IconType.None);

public static class SelectItemExtensions
{
    /// <summary>
    /// Turns any collection into choices for a <see cref="Select{TValue}"/>:
    /// <c>Items="@games.ToSelectItems(g =&gt; g.Id, g =&gt; g.Title)"</c>.
    /// </summary>
    public static IEnumerable<SelectItem<TValue>> ToSelectItems<TItem, TValue>(
        this IEnumerable<TItem> items,
        Func<TItem, TValue> value,
        Func<TItem, string?> label,
        Func<TItem, bool>? disabled = null,
        Func<TItem, string?>? image = null) =>
        items.Select(item => new SelectItem<TValue>(value(item), label(item) ?? "", disabled?.Invoke(item) ?? false, image?.Invoke(item)));
}
