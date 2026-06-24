#include "settings.h"

#include <windows.h>
#include <cstdio>

std::string Settings::FilePath() const
{
    char buf[MAX_PATH];
    DWORD n = GetModuleFileNameA(NULL, buf, MAX_PATH);
    if (n == 0) return std::string("settings.ini");
    std::string path(buf, n);
    size_t slash = path.find_last_of("\\/");
    if (slash == std::string::npos) return std::string("settings.ini");
    return path.substr(0, slash + 1) + "settings.ini";
}

void Settings::Load()
{
    Clear();
    FILE* f = fopen(FilePath().c_str(), "rb");
    if (!f) return;
    char line[2048];
    while (fgets(line, sizeof(line), f))
    {
        std::string s(line);
        while (!s.empty() && (s[s.size()-1] == '\n' || s[s.size()-1] == '\r'))
            s.erase(s.size() - 1);
        size_t eq = s.find('=');
        if (eq == std::string::npos) continue;
        std::string key = s.substr(0, eq);
        std::string val = s.substr(eq + 1);
        if      (key == "serverUrl")         serverUrl         = val;
        else if (key == "userName")          userName          = val;
        else if (key == "accessToken")       accessToken       = val;
        else if (key == "refreshToken")      refreshToken      = val;
        else if (key == "expiration")        expiration        = val;
        else if (key == "alias")             alias             = val;
        else if (key == "showLibraryOnly")   showLibraryOnly   = (val == "1");
        else if (key == "showInstalledOnly") showInstalledOnly = (val == "1");
        else if (key == "filterGenre")       filterGenre       = val;
        else if (key == "viewMode")
        {
            int v = atoi(val.c_str());
            if (v >= 0 && v <= 2) viewMode = v;
        }
        else if (key == "defaultInstallDir") defaultInstallDir = val;
        else if (key == "discoveryTimeoutMs")
        {
            int n = atoi(val.c_str());
            if (n >= 500 && n <= 30000) discoveryTimeoutMs = n;
        }
    }
    fclose(f);
}

void Settings::Save()
{
    FILE* f = fopen(FilePath().c_str(), "wb");
    if (!f) return;
    fprintf(f, "serverUrl=%s\r\n",         serverUrl.c_str());
    fprintf(f, "userName=%s\r\n",          userName.c_str());
    fprintf(f, "accessToken=%s\r\n",       accessToken.c_str());
    fprintf(f, "refreshToken=%s\r\n",      refreshToken.c_str());
    fprintf(f, "expiration=%s\r\n",        expiration.c_str());
    fprintf(f, "alias=%s\r\n",             alias.c_str());
    fprintf(f, "showLibraryOnly=%d\r\n",   showLibraryOnly ? 1 : 0);
    fprintf(f, "showInstalledOnly=%d\r\n", showInstalledOnly ? 1 : 0);
    fprintf(f, "filterGenre=%s\r\n",       filterGenre.c_str());
    fprintf(f, "viewMode=%d\r\n",          viewMode);
    fprintf(f, "defaultInstallDir=%s\r\n", defaultInstallDir.c_str());
    fprintf(f, "discoveryTimeoutMs=%d\r\n", discoveryTimeoutMs);
    fclose(f);
}

void Settings::Clear()
{
    serverUrl.clear();
    userName.clear();
    accessToken.clear();
    refreshToken.clear();
    expiration.clear();
    alias.clear();
    showLibraryOnly   = true;
    showInstalledOnly = false;
    filterGenre.clear();
    viewMode = 0;
    defaultInstallDir.clear();
    discoveryTimeoutMs = 2000;
}
