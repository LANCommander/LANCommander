#include "save_client.h"

#include <cstdio>
#include <sstream>

bool SaveClient::UploadSave(const std::string& gameId, const std::string& zipPath,
                            std::string* errorOut)
{
    HttpResponse resp = m_http.PostMultipartFile(
        "/api/Saves/Game/" + gameId + "/Upload", "file", zipPath);
    if (resp.ok()) return true;
    if (errorOut)
    {
        std::ostringstream e;
        e << "Save upload failed (HTTP " << resp.status << ")";
        *errorOut = e.str();
    }
    return false;
}

bool SaveClient::DownloadLatestSave(const std::string& gameId,
                                    const std::string& destZipPath,
                                    DownloadProgressFn progress, void* userData,
                                    std::string* errorOut)
{
    FILE* f = fopen(destZipPath.c_str(), "wb");
    if (!f)
    {
        if (errorOut) *errorOut = "Could not open destination file";
        return false;
    }
    long status = m_http.Download(
        "/api/Saves/Game/" + gameId + "/Latest/Download", f, progress, userData);
    fclose(f);

    if (status == 404)
    {
        ::remove(destZipPath.c_str());
        if (errorOut) *errorOut = "No save on server";
        return false;
    }
    if (status < 200 || status >= 300)
    {
        ::remove(destZipPath.c_str());
        if (errorOut)
        {
            std::ostringstream e;
            e << "Save download failed (HTTP " << status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    return true;
}
