#ifndef LANCOMMANDER_WIN9X_REDISTRIBUTABLE_CLIENT_H
#define LANCOMMANDER_WIN9X_REDISTRIBUTABLE_CLIENT_H

#include <string>

#include "http_client.h"

// Mirrors LANCommander.SDK.Clients.RedistributableClient. Owns endpoints
// rooted at /api/Redistributables. We deliberately skip Scripts and the
// SDK's detect-install hook — Win9x doesn't run PowerShell, so the user is
// responsible for deciding which prereqs to (re)install.
class RedistributableClient
{
public:
    explicit RedistributableClient(HttpClient& http) : m_http(http) {}

    bool Download(const std::string& redistId, const std::string& destPath,
                  DownloadProgressFn progress, void* userData,
                  std::string* errorOut);

private:
    HttpClient& m_http;
};

#endif
