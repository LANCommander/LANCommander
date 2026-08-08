#ifndef LANCOMMANDER_MODELS_SERVER_DETAIL_H
#define LANCOMMANDER_MODELS_SERVER_DETAIL_H

#include <string>
#include <vector>

#include "game.h"
#include "script.h"

namespace lancommander {

enum class ProcessTerminationMethod {
    Close = 0,
    Kill,
    SIGHUP,
    SIGINT,
    SIGKILL,
    SIGTERM
};

enum class ServerAutostartMethod {
    OnApplicationStart = 0,
    OnPlayerActivity
};

enum class ServerConsoleType {
    LogFile = 0,
    RCON
};

struct ServerConsole {
    std::string id;
    std::string name;
    ServerConsoleType type = ServerConsoleType::LogFile;
};

struct ServerHttpPath {
    std::string id;
    std::string path;
};

struct ServerDetail {
    std::string id;
    std::string name;
    std::string path;
    std::string arguments;
    std::string working_directory;
    std::string host;
    int port = 0;
    bool use_shell_execute = false;
    ProcessTerminationMethod process_termination_method = ProcessTerminationMethod::Close;
    bool autostart = false;
    ServerAutostartMethod autostart_method = ServerAutostartMethod::OnApplicationStart;
    int autostart_delay = 0;
    std::string game_id;
    std::vector<ServerConsole> server_consoles;
    std::vector<ServerHttpPath> http_paths;
    std::vector<Script> scripts;
    std::string created_on;
    std::string updated_on;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_SERVER_DETAIL_H
