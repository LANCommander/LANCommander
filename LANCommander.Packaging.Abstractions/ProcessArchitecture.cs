namespace LANCommander.Packaging;

/// <summary>
/// Machine architecture of a process or executable image.
/// <para>
/// This drives worker routing. DLL injection via CreateRemoteThread/LoadLibraryW only works
/// when the injecting process and the target process share a bitness, so every discovered
/// process has to be classified before anything can be injected into it.
/// </para>
/// </summary>
public enum ProcessArchitecture
{
    /// <summary>The image could not be read or its machine type was not recognized.</summary>
    Unknown = 0,

    /// <summary>32-bit x86 (PE machine 0x014C).</summary>
    X86 = 1,

    /// <summary>64-bit x86 (PE machine 0x8664).</summary>
    X64 = 2,

    /// <summary>
    /// 64-bit ARM (PE machine 0xAA64). No Interposer build exists for this, so targets
    /// reporting it cannot be instrumented.
    /// </summary>
    Arm64 = 3,
}
