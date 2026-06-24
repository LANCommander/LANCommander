#include "path_expand.h"

#include <windows.h>
#include <shlobj.h>

namespace
{
    // SHGetSpecialFolderPathA isn't in stock Win95 — IE 4.0+ ships it as part
    // of shell32. Resolve dynamically so the binary still loads on a bare
    // Win95 box; the function just becomes a no-op there.
    typedef BOOL (WINAPI *SHGSFP_t)(HWND, LPSTR, int, BOOL);
    SHGSFP_t ResolveSHGSFP()
    {
        static SHGSFP_t fn   = NULL;
        static bool     done = false;
        if (done) return fn;
        done = true;
        HMODULE m = LoadLibraryA("shell32.dll");
        if (m) fn = (SHGSFP_t)GetProcAddress(m, "SHGetSpecialFolderPathA");
        return fn;
    }

    std::string FolderPath(int csidl)
    {
        SHGSFP_t fn = ResolveSHGSFP();
        if (!fn) return std::string();
        char buf[MAX_PATH];
        if (!fn(NULL, buf, csidl, FALSE)) return std::string();
        return std::string(buf);
    }

    struct Mapping
    {
        const char* token;
        int         csidl;
    };

    // The full SDK uses .NET's Environment.SpecialFolder enum names verbatim.
    // We map those to CSIDL values. The Common* entries are NT-only; on Win9x
    // they fall back to the per-user equivalent, which is what makes sense
    // since 9x has no per-user/all-users split.
    const Mapping kMappings[] =
    {
        { "MyDocuments",            CSIDL_PERSONAL },
        { "Desktop",                CSIDL_DESKTOP },
        { "DesktopDirectory",       CSIDL_DESKTOPDIRECTORY },
        { "Fonts",                  CSIDL_FONTS },
        { "Programs",               CSIDL_PROGRAMS },
        { "StartMenu",              CSIDL_STARTMENU },
        { "Startup",                CSIDL_STARTUP },
        { "AppData",                CSIDL_APPDATA },
        { "ApplicationData",        CSIDL_APPDATA },
        { "LocalApplicationData",   CSIDL_APPDATA },
        { "MyMusic",                CSIDL_MYMUSIC },
        { "MyPictures",             CSIDL_MYPICTURES },
        { "MyVideos",               CSIDL_MYVIDEO },
        { "CommonApplicationData",  CSIDL_APPDATA },
        { "CommonDocuments",        CSIDL_PERSONAL },
        { "CommonDesktopDirectory", CSIDL_DESKTOPDIRECTORY },
        { "CommonPrograms",         CSIDL_PROGRAMS },
        { "CommonStartMenu",        CSIDL_STARTMENU },
        { "CommonStartup",          CSIDL_STARTUP },
        { "CommonMusic",            CSIDL_MYMUSIC },
        { "CommonPictures",         CSIDL_MYPICTURES },
        { "CommonVideos",           CSIDL_MYVIDEO },
    };
}

std::string ExpandSpecialFolders(const std::string& in)
{
    if (in.find('%') == std::string::npos) return in;
    std::string out = in;
    for (size_t i = 0; i < sizeof(kMappings) / sizeof(kMappings[0]); ++i)
    {
        const Mapping& m = kMappings[i];
        std::string token = "%";
        token += m.token;
        token += "%";
        if (out.find(token) == std::string::npos) continue;
        std::string path = FolderPath(m.csidl);
        if (path.empty()) continue;
        size_t pos = 0;
        while ((pos = out.find(token, pos)) != std::string::npos)
        {
            out.replace(pos, token.size(), path);
            pos += path.size();
        }
    }
    return out;
}
