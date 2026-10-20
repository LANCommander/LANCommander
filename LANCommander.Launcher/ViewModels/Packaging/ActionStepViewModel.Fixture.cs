#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace LANCommander.Launcher.ViewModels.Packaging;

public partial class ActionStepViewModel
{
    /// <summary>
    /// Sets the packaged files for a visual fixture from paths already relative to the install
    /// folder. <see cref="OnEnterAsync"/> works them out from absolute Windows paths, which don't
    /// resolve on other platforms.
    /// </summary>
    internal void SeedFilesForFixture(IEnumerable<string> relativePaths)
    {
        _packagedFiles = relativePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allExecutables = _packagedFiles
            .Where(p => ExecutableExtensions.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PopulateExecutables();
    }

    /// <summary>Adds a row for a visual fixture and refreshes what depends on it.</summary>
    internal ActionEntryViewModel AddActionForFixture(string name, string path, string arguments = "", bool isPrimary = false)
    {
        var action = new ActionEntryViewModel(this)
        {
            Name = name,
            Path = path,
            Arguments = arguments,
            WorkingDirectory = InstallDirectoryVariable,
        };

        Actions.Add(action);

        action.IsPrimary = isPrimary;
        action.RefreshDerivedState();

        OnPropertyChanged(nameof(HasActions));
        UpdateSummary();

        return action;
    }
}
#endif
