using System;

namespace LANCommander.SDK.Plugins.Events;

/// <summary>Raised just before a game install begins.</summary>
public sealed record GameInstallingEvent(Guid GameId, string? InstallDirectory);

/// <summary>Raised after a game has finished installing.</summary>
public sealed record GameInstalledEvent(Guid GameId, string InstallDirectory);

/// <summary>Raised when a game install fails.</summary>
public sealed record GameInstallFailedEvent(Guid GameId, string? InstallDirectory);

/// <summary>Raised just before a game is uninstalled.</summary>
public sealed record GameUninstallingEvent(Guid GameId, string? InstallDirectory);

/// <summary>Raised after a game has finished uninstalling.</summary>
public sealed record GameUninstalledEvent(Guid GameId);

/// <summary>Raised immediately before a game's executable is launched.</summary>
public sealed record GameBeforeLaunchEvent(Guid GameId, string InstallDirectory, string? Action);

/// <summary>Raised immediately after a launched game process exits.</summary>
public sealed record GameAfterExitEvent(Guid GameId, string InstallDirectory);

/// <summary>Raised whenever the install/download queue changes.</summary>
public sealed record InstallQueueChangedEvent;
