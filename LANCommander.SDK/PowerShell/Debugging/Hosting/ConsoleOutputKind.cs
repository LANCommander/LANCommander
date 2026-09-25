namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>Classification of a line arriving in the debugger console. Drives colouring.</summary>
public enum ConsoleOutputKind
{
    /// <summary>Success stream: Write-Output and bare expressions.</summary>
    Output,

    /// <summary>Write-Host, routed through the host UI.</summary>
    Host,

    Error,
    Warning,
    Verbose,
    Debug,
    Information,

    /// <summary>A prompt emitted by the engine (Read-Host, PromptForChoice).</summary>
    Prompt,

    /// <summary>Echo of something the user typed into the console input box.</summary>
    Echo,

    /// <summary>A message from the debugger itself, not from the script.</summary>
    System,
}
