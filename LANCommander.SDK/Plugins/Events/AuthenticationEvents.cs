using System;

namespace LANCommander.SDK.Plugins.Events;

/// <summary>Raised after a user successfully logs in.</summary>
public sealed record UserLoggedInEvent(Guid UserId, string UserName);

/// <summary>Raised after a user logs out.</summary>
public sealed record UserLoggedOutEvent(Guid UserId, string UserName);
