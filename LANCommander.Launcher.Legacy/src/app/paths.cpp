#include "app/paths.h"

#ifdef _WIN32
#include <windows.h>
#elif defined(__DJGPP__)
#include <crt0.h>
#endif

namespace launcher
{

    namespace
    {
        std::string resolve_exe_dir()
        {
#ifdef _WIN32
            // MAX_PATH is the right size here: this is the launcher's own
            // install directory, not a user-supplied path, and the Win9x
            // target has no long-path support to take advantage of anyway.
            char buf[MAX_PATH];
            const DWORD n = GetModuleFileNameA(NULL, buf, MAX_PATH);

            if (n == 0 || n >= MAX_PATH)
                return std::string();

            std::string path(buf, (size_t)n);

            const size_t slash = path.find_last_of("\\/");
            if (slash == std::string::npos)
                return std::string();

            return path.substr(0, slash + 1);
#elif defined(__DJGPP__)
            // DJGPP's startup code resolves the program's own path through
            // the DOS environment and leaves it here, drive letter included,
            // so this is the direct equivalent of GetModuleFileNameA. argv[0]
            // is not: DOS passes the command tail only, and everything before
            // the program name is the shell's reconstruction of it.
            if (!__dos_argv0 || !*__dos_argv0)
                return std::string();

            std::string path(__dos_argv0);

            const size_t slash = path.find_last_of("\\/");
            if (slash == std::string::npos)
                return std::string();

            return path.substr(0, slash + 1);
#else
            // No portable answer, and no non-Windows target yet. Returning
            // empty keeps the old cwd-relative behaviour rather than failing.
            return std::string();
#endif
        }
    } // namespace

    const std::string &exe_dir()
    {
        // Resolved once. The executable cannot move while it is running.
        static const std::string dir = resolve_exe_dir();
        return dir;
    }

    std::string app_path(const char *relative)
    {
        if (!relative)
            return exe_dir();

        return exe_dir() + relative;
    }

} // namespace launcher
