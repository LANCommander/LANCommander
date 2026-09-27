namespace LANCommander.Server.UI.Controls;

public enum TreeDropPosition
{
    /// <summary>Directly above the target, as its previous sibling.</summary>
    Before,
    /// <summary>Into the target, as a child.</summary>
    Inside,
    /// <summary>Directly below the target, as its next sibling.</summary>
    After,
}

/// <summary>An item dragged onto another in a <see cref="Tree{TItem}"/>.</summary>
public sealed record TreeDrop<TItem>(TItem Item, TItem Target, TreeDropPosition Position);
