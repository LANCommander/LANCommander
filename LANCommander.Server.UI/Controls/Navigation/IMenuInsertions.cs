using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// Items a <see cref="Menu"/> places among the ones written in its markup, e.g. entries added by
/// plugins. A group is the menu's top level (null) or a <see cref="SubMenu"/> with a
/// <see cref="SubMenu.Name"/>; an anchor is an item's <see cref="MenuItem.Href"/> or a group's name.
/// </summary>
public interface IMenuInsertions
{
    /// <summary>Items placed first in <paramref name="group"/>.</summary>
    RenderFragment? Start(string? group);

    /// <summary>Items placed right after the item or group <paramref name="anchor"/> in <paramref name="group"/>.</summary>
    RenderFragment? After(string? group, string anchor);

    /// <summary>
    /// Items placed last in <paramref name="group"/>. <paramref name="anchors"/> are the anchors the
    /// group rendered, so items whose anchor is missing can be placed here instead of being lost.
    /// </summary>
    RenderFragment? End(string? group, IReadOnlySet<string> anchors);
}
