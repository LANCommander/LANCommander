#ifndef LANCOMMANDER_MODELS_SCRIPT_H
#define LANCOMMANDER_MODELS_SCRIPT_H

#include <string>

namespace lancommander {

enum class ScriptType {
    Install = 0,
    Uninstall,
    NameChange,
    KeyChange,
    SaveUpload,
    SaveDownload,
    DetectInstall,
    BeforeStart,
    AfterStop,
    GameStarted,
    GameStopped,
    UserRegistration,
    UserLogin,
    ApplicationStart,
    Package,
    RunWrapper,
    Unknown = -1
};

// Bit flags mirroring the .NET [Flags] RuntimePlatform enum. None (0) means
// "unspecified", which is treated as "runs everywhere" for backwards
// compatibility — see script::supports_current_runtime().
//
// Deliberately an unscoped enum stored in an int: C++14 `enum class` has no
// built-in bitwise operators, and the operator overloads needed to add them
// are handled inconsistently by Open Watcom and VC6.
enum RuntimePlatform {
    RuntimePlatform_None    = 0,
    RuntimePlatform_Windows = 1 << 0,
    RuntimePlatform_Linux   = 1 << 1,
    RuntimePlatform_macOS   = 1 << 2
};

struct Script {
    ScriptType type = ScriptType::Unknown;
    std::string name;
    std::string description;
    std::string contents;
    // The C++ runner cannot elevate: picoposh parses
    // "#Requires -RunAsAdministrator" as an ordinary comment. This flag is
    // preserved so the host can decide whether to elevate itself or refuse,
    // and so scripts written to disk stay byte-compatible with the .NET SDK.
    bool requires_admin = false;
    int platforms = RuntimePlatform_None;  // bitmask of RuntimePlatform
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_SCRIPT_H
