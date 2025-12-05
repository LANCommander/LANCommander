using System;
using System.Collections.Generic;
using System.Linq;

namespace LANCommander.SDK.Services;

public class GameInstallationFileListEntry
{
    public class FileEntry
    {
        public string EntryPath { get; set; }
        public string LocalPath { get; set; }
    }

    public Guid GameId { get; internal set; }

    public Models.Manifest.Game Manifest { get; internal set; }
    public List<FileEntry> Files { get; private set; } = [];

    public void Merge(GameInstallationFileListEntry otherInfo)
    {
        Manifest ??= otherInfo.Manifest;

        var newFiles = otherInfo?.Files?.ExceptBy(Files.Select(entry => entry.EntryPath), entry => entry.EntryPath, StringComparer.OrdinalIgnoreCase) ?? [];
        Files.AddRange(newFiles);
    }

    internal void AddFiles(IEnumerable<FileEntry> gameFiles)
    {
        Files.AddRange(gameFiles ?? []);
    }

    internal void AddFile(FileEntry gameFile)
    {
        ArgumentNullException.ThrowIfNull(gameFile);

        Files.Add(gameFile);
    }
}

public class GameInstallationFileList
{
    public static GameInstallationFileList Empty => new();

    protected GameInstallationFileList()
    {
    }

    public GameInstallationFileList(string installDirectory, Guid gameId)
    {
        BaseGame.GameId = gameId;
        InstallDirectory = installDirectory;
    }

    public string InstallDirectory { get; internal set; }

    public GameInstallationFileListEntry BaseGame { get; internal set; } = new();
    public Dictionary<Guid, GameInstallationFileList> DependentGames { get; private set; } = [];

    public void Merge(GameInstallationFileList gameFileList)
    {
        MergeBase(gameFileList);
        MergeDependentGames(gameFileList);
    }

    public void MergeBase(GameInstallationFileList gameFileList)
    {
        BaseGame.Merge(gameFileList.BaseGame);
    }

    public void MergeBaseAsDependentGame(Guid gameId, GameInstallationFileList gameFileList)
    {
        if (!DependentGames.TryGetValue(gameId, out var depFileList))
        {
            depFileList = new(gameFileList.InstallDirectory, gameId);
            DependentGames.Add(gameId, depFileList);
            depFileList.BaseGame.Manifest = gameFileList.BaseGame.Manifest;
        }

        depFileList.BaseGame.Merge(gameFileList.BaseGame);
    }

    public void MergeDependentGames(GameInstallationFileList gameFileList)
    {
        foreach ((var otherDependentGameId, var otherDependentFileList) in gameFileList?.DependentGames ?? [])
        {
            if (!DependentGames.TryGetValue(otherDependentGameId, out var depFileList))
            {
                depFileList = new(gameFileList.InstallDirectory, otherDependentGameId);
                DependentGames.Add(otherDependentGameId, depFileList);
                //depFileList.BaseGame.Manifest = otherDependentFileList.;
            }

            depFileList.MergeDependentGames(otherDependentFileList);
        }
    }

    public IEnumerable<GameInstallationFileListEntry.FileEntry> ToFlatDistinctFileEntries()
    {
        return BaseGame.Files
            .Concat(DependentGames.Values.SelectMany(dep => dep.BaseGame.Files))
            .GroupBy(file => file.EntryPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()); // Ensures distinct entries by EntryPath
    }
}
