#include "logger.h"

#include <windows.h>

#include <cstdarg>
#include <cstdio>
#include <string>

namespace
{
    std::string LogPath()
    {
        char buf[MAX_PATH];
        DWORD n = GetModuleFileNameA(NULL, buf, MAX_PATH);
        std::string path = (n == 0) ? std::string(".") : std::string(buf, n);
        size_t slash = path.find_last_of("\\/");
        if (slash != std::string::npos) path = path.substr(0, slash);
        return path + "\\errors.log";
    }
}

void LogError(const char* fmt, ...)
{
    FILE* f = fopen(LogPath().c_str(), "ab");
    if (!f) return;

    SYSTEMTIME st;
    GetLocalTime(&st);
    fprintf(f, "%04d-%02d-%02d %02d:%02d:%02d  ",
            st.wYear, st.wMonth, st.wDay,
            st.wHour, st.wMinute, st.wSecond);

    va_list args;
    va_start(args, fmt);
    vfprintf(f, fmt, args);
    va_end(args);

    fputs("\r\n", f);
    fclose(f);
}
