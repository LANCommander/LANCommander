using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Adds a place to import records from to the server's import dialog, alongside the built-in Upload
/// and Local File, e.g. a store library to pick games from. Register an implementation in
/// <c>IPlugin.ConfigureServices</c>; any number of plugins may, one source per implementation.
/// </summary>
/// <remarks>
/// <para>
/// The dialog lists the source by its <see cref="Name"/> in the switcher at its top, when importing
/// one of its <see cref="SupportedTypes"/>, and shows its <see cref="Component"/> below the switcher
/// when it is chosen. The component is given an <see cref="ImportSourceContext"/> as its
/// <c>Context</c> parameter, so declare <c>[Parameter] public ImportSourceContext Context { get; set; }</c>.
/// It draws its own controls, including the button that starts the import; the dialog adds Cancel.
/// </para>
/// <para>
/// The component finishes in one of two ways. Most write an LCX package and hand it to
/// <see cref="ImportSourceContext.ImportPackageAsync"/>, which reads it and lets the administrator
/// choose its records, as for an upload. Others import through <see cref="IPackageImporter"/> or the
/// record providers themselves, then call <see cref="ImportSourceContext.CompleteAsync"/>.
/// </para>
/// <para>
/// The extension is a singleton, asked whenever the dialog opens, so its members should be cheap.
/// The component is shown to administrators only, and a component that throws shows the error in
/// place of itself without affecting the other sources.
/// </para>
/// </remarks>
public interface IImportSourceExtension
{
    /// <summary>
    /// Identifies the source; keep it stable: letters, digits, <c>.</c>, <c>-</c> and <c>_</c>, e.g.
    /// <c>com.mycompany.myplugin.library</c>.
    /// </summary>
    string Id { get; }

    /// <summary>The name the switcher lists the source by, e.g. "GOG". Keep it short.</summary>
    string Name { get; }

    /// <summary>
    /// The kinds of record the source can import, e.g. only <see cref="ManifestType.Game"/>. The
    /// source is only offered when importing one of them.
    /// </summary>
    IReadOnlyCollection<ManifestType> SupportedTypes { get; }

    /// <summary>
    /// The Razor component shown below the switcher, taking the <see cref="ImportSourceContext"/> as
    /// its <c>Context</c> parameter. An extension whose component doesn't is left out.
    /// </summary>
    Type Component { get; }
}

/// <summary>
/// What an <see cref="IImportSourceExtension"/>'s component is given as its <c>Context</c> parameter:
/// what is being imported, and how to hand the import back to the dialog.
/// </summary>
public sealed class ImportSourceContext
{
    private readonly Func<Guid> _storageLocationId;
    private readonly Func<string, bool, Task> _importPackage;
    private readonly Func<IReadOnlyList<string>, Task> _complete;
    private readonly Func<Task> _close;

    /// <summary>Created by the server's import dialog; plugins only need it to test their components.</summary>
    public ImportSourceContext(
        ManifestType? manifestType,
        Func<Guid> storageLocationId,
        Func<string, bool, Task> importPackage,
        Func<IReadOnlyList<string>, Task> complete,
        Func<Task> close,
        CancellationToken cancellationToken)
    {
        ManifestType = manifestType;
        _storageLocationId = storageLocationId;
        _importPackage = importPackage;
        _complete = complete;
        _close = close;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// The kind of record being imported, one of the extension's
    /// <see cref="IImportSourceExtension.SupportedTypes"/>. Null when the dialog wasn't opened for one
    /// kind in particular.
    /// </summary>
    public ManifestType? ManifestType { get; }

    /// <summary>
    /// The archive storage location the administrator chose below the source, for an import the
    /// component runs itself. <see cref="Guid.Empty"/> means the default archive location.
    /// </summary>
    public Guid StorageLocationId => _storageLocationId();

    /// <summary>Cancelled when the dialog closes; stop downloading or packaging then.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Hands the dialog an LCX package on the server's disk, which it reads and lists the records of
    /// for the administrator to choose from, as for an upload. A package that can't be read is
    /// reported and leaves the source shown.
    /// </summary>
    /// <param name="packagePath">Path to the package on the server.</param>
    /// <param name="deleteWhenDone">
    /// Whether the dialog deletes the package once it is done with it, e.g. one the component wrote
    /// to a temporary folder.
    /// </param>
    public Task ImportPackageAsync(string packagePath, bool deleteWhenDone = false) => _importPackage(packagePath, deleteWhenDone);

    /// <summary>
    /// Tells the dialog the component has imported what it was asked to itself, so it shows the
    /// import as complete, with any warnings, e.g. records it skipped.
    /// </summary>
    public Task CompleteAsync(IReadOnlyList<string>? warnings = null) => _complete(warnings ?? []);

    /// <summary>Closes the dialog without importing anything.</summary>
    public Task CloseAsync() => _close();
}
