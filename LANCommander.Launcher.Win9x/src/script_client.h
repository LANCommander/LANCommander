#ifndef LANCOMMANDER_WIN9X_SCRIPT_CLIENT_H
#define LANCOMMANDER_WIN9X_SCRIPT_CLIENT_H

#include <string>
#include <vector>

#include "http_client.h"

struct Script
{
    // Subset of LANCommander.SDK.Enums.ScriptType. We only wire up the hooks
    // that make sense on Win9x without PowerShell. The integer values match
    // the SDK enum so we can decode a number-typed payload directly.
    enum Type
    {
        TypeUnknown     = -1,
        TypeInstall     = 0,
        TypeUninstall   = 1,
        TypeNameChange  = 2,
        TypeKeyChange   = 3,
        TypeBeforeStart = 7,
        TypeAfterStop   = 8
    };

    Type        type;
    std::string name;
    std::string contents;

    Script() : type(TypeUnknown) {}
};

// Mirrors LANCommander.SDK.Clients.ScriptClient. Win9x consumes only the
// game and redistributable script endpoints; tool/auth scripts are skipped.
class ScriptClient
{
public:
    explicit ScriptClient(HttpClient& http) : m_http(http) {}

    bool GetGameScripts(const std::string& gameId,
                        std::vector<Script>* out, std::string* errorOut);

    bool GetRedistributableScripts(const std::string& redistId,
                                   std::vector<Script>* out,
                                   std::string* errorOut);

private:
    bool FetchAndParse(const std::string& route,
                       std::vector<Script>* out, std::string* errorOut);

    HttpClient& m_http;
};

#endif
