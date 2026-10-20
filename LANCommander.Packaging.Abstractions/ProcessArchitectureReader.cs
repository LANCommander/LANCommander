using System.Runtime.InteropServices;

namespace LANCommander.Packaging;

/// <summary>
/// Classifies an executable image so the session knows which worker can inject into it.
/// </summary>
public static class ProcessArchitectureReader
{
    /// <summary>
    /// Reads the architecture of the executable at <paramref name="imagePath"/>.
    /// </summary>
    /// <returns>
    /// <see cref="ProcessArchitecture.Unknown"/> when the image is missing, locked or not a PE
    /// file — callers decide what to do rather than being handed a wrong answer.
    /// </returns>
    public static ProcessArchitecture FromImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return ProcessArchitecture.Unknown;

        return FromMachineType(PeMachineReader.Read(imagePath));
    }

    public static ProcessArchitecture FromMachineType(PeMachineType machine) => machine switch
    {
        PeMachineType.I386 => ProcessArchitecture.X86,
        PeMachineType.Amd64 => ProcessArchitecture.X64,
        PeMachineType.Arm64 => ProcessArchitecture.Arm64,
        _ => ProcessArchitecture.Unknown,
    };

    /// <summary>
    /// Architecture of the currently running process.
    /// </summary>
    public static ProcessArchitecture Current => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => ProcessArchitecture.X86,
        Architecture.X64 => ProcessArchitecture.X64,
        Architecture.Arm64 => ProcessArchitecture.Arm64,
        _ => ProcessArchitecture.Unknown,
    };

    /// <summary>
    /// The runtime identifier of the worker able to inject into <paramref name="architecture"/>,
    /// or null when no worker can — there is no ARM64 Interposer build, so ARM64 targets cannot
    /// be instrumented at all.
    /// </summary>
    public static string? GetWorkerRuntimeIdentifier(ProcessArchitecture architecture) => architecture switch
    {
        ProcessArchitecture.X86 => "win-x86",
        ProcessArchitecture.X64 => "win-x64",
        _ => null,
    };
}
