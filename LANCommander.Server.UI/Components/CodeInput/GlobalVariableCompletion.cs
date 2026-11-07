namespace LANCommander.Server.UI.Components;

/// <summary>A global script variable the PowerShell editor completes after <c>$</c>; <see cref="Name"/> has no <c>$</c>.</summary>
public record GlobalVariableCompletion(string Name, string? Description);
