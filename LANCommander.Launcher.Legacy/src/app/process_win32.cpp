// process_win32.cpp — game launching on Windows.
//
// ShellExecuteExA rather than CreateProcessA, lifted unchanged from
// screen_game_detail.cpp: the "open" verb is what makes a non-executable
// action target (a .bat, a shortcut, a document) work the way it does from
// Explorer.

#include "app/process.h"

#include <windows.h>
#include <shellapi.h>

#include <cstdio>

namespace launcher
{

    void *process_start(const std::string &path, const std::string &args,
                        const std::string &workdir, std::string *error)
    {
        SHELLEXECUTEINFOA info;
        ZeroMemory(&info, sizeof(info));
        info.cbSize = sizeof(info);
        info.fMask = SEE_MASK_FLAG_NO_UI | SEE_MASK_NOCLOSEPROCESS;
        info.lpVerb = "open";
        info.lpFile = path.c_str();
        info.lpParameters = args.empty() ? NULL : args.c_str();
        info.lpDirectory = workdir.empty() ? NULL : workdir.c_str();
        info.nShow = SW_SHOWNORMAL;

        if (!ShellExecuteExA(&info))
        {
            if (error)
            {
                char buf[128];
                sprintf(buf, "Launch failed (error %lu)", GetLastError());
                *error = buf;
            }
            return NULL;
        }

        return (void *)info.hProcess;
    }

    bool process_running(void *handle)
    {
        if (!handle)
            return false;
        return WaitForSingleObject((HANDLE)handle, 0) != WAIT_OBJECT_0;
    }

    void process_terminate(void *handle)
    {
        if (handle)
            TerminateProcess((HANDLE)handle, 0);
    }

    void process_close(void *handle)
    {
        if (handle)
            CloseHandle((HANDLE)handle);
    }

    bool process_concurrent() { return true; }

} // namespace launcher
