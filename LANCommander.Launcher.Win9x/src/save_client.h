#ifndef LANCOMMANDER_WIN9X_SAVE_CLIENT_H
#define LANCOMMANDER_WIN9X_SAVE_CLIENT_H

#include <string>

#include "http_client.h"

// Mirrors LANCommander.SDK.Clients.SaveClient. Operates against
// /api/Saves/Game/{id}/...
class SaveClient
{
public:
    explicit SaveClient(HttpClient& http) : m_http(http) {}

    bool UploadSave(const std::string& gameId, const std::string& zipPath,
                    std::string* errorOut);

    bool DownloadLatestSave(const std::string& gameId,
                            const std::string& destZipPath,
                            DownloadProgressFn progress, void* userData,
                            std::string* errorOut);

private:
    HttpClient& m_http;
};

#endif
