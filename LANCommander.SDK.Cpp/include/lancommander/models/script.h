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

struct Script {
    ScriptType type = ScriptType::Unknown;
    std::string name;
    std::string contents;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_SCRIPT_H
