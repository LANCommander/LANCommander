#include "redistributable_client.h"

#include <cstdio>
#include <sstream>

bool RedistributableClient::Download(const std::string& redistId,
                                     const std::string& destPath,
                                     DownloadProgressFn progress,
                                     void* userData,
                                     std::string* errorOut)
{
    FILE* f = fopen(destPath.c_str(), "wb");
    if (!f)
    {
        if (errorOut) *errorOut = "Could not open destination file";
        return false;
    }
    long status = m_http.Download("/api/Redistributables/" + redistId + "/Download",
                                  f, progress, userData);
    fclose(f);
    if (status < 200 || status >= 300)
    {
        ::remove(destPath.c_str());
        if (errorOut)
        {
            std::ostringstream e;
            e << "Redistributable download failed (HTTP " << status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    return true;
}
