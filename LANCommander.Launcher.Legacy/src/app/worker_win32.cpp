// worker_win32.cpp — background jobs on threads, for the Windows builds.
//
// A straight lift of the _beginthreadex / CRITICAL_SECTION code that used to
// be repeated in download_queue.cpp, media_prefetch.cpp and
// game_art_fetcher.cpp.

#include "app/worker.h"

#include <windows.h>
#include <process.h>

namespace launcher
{

    namespace
    {
        struct Call
        {
            WorkerFn fn;
            void *arg;
        };

        // _beginthreadex needs __stdcall and an unsigned return, which
        // WorkerFn deliberately does not have — the DOS side would have no
        // use for either.
        unsigned __stdcall trampoline(void *param)
        {
            Call *c = (Call *)param;
            const WorkerFn fn = c->fn;
            void *const arg = c->arg;
            delete c;

            fn(arg);
            return 0;
        }
    } // namespace

    void *worker_start(WorkerFn fn, void *arg)
    {
        if (!fn)
            return NULL;

        Call *c = new Call;
        c->fn = fn;
        c->arg = arg;

        unsigned thread_id = 0;
        HANDLE h = (HANDLE)_beginthreadex(NULL, 0, trampoline, c, 0, &thread_id);

        if (!h)
        {
            delete c;
            return NULL;
        }

        return (void *)h;
    }

    void worker_join(void *handle)
    {
        if (!handle)
            return;
        WaitForSingleObject((HANDLE)handle, INFINITE);
        CloseHandle((HANDLE)handle);
    }

    bool worker_concurrent() { return true; }

    void worker_set_background_priority()
    {
        // Win9x only, and deliberately so.
        //
        // The reason to deprioritise a download worker at all is the
        // Win16Mutex: on 9x a file write thunks down to 16-bit VFAT and takes
        // that lock, GDI needs the same lock, and a worker that never yields
        // it is a launcher that stops redrawing. NT has no such coupling.
        //
        // On NT this was actively harmful. A single-core Pentium 4 running
        // the launcher's 30 FPS loop has a normal-priority thread runnable
        // almost all of the time, and a BELOW_NORMAL download loses to it on
        // every scheduling decision -- which showed up as a transfer capped
        // around 1 MB/s on hardware that should saturate a gigabit link.
        OSVERSIONINFOA os;
        ZeroMemory(&os, sizeof(os));
        os.dwOSVersionInfoSize = sizeof(os);

        if (!GetVersionExA(&os) || os.dwPlatformId != VER_PLATFORM_WIN32_WINDOWS)
            return;

        // BELOW_NORMAL rather than IDLE: an idle-priority download on a busy
        // machine can be starved outright, and the point is to lose ties to
        // the UI, not to stop.
        SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL);
    }

    void *worker_mutex_create()
    {
        CRITICAL_SECTION *cs = new CRITICAL_SECTION;
        InitializeCriticalSection(cs);
        return cs;
    }

    void worker_mutex_destroy(void *mutex)
    {
        if (!mutex)
            return;
        DeleteCriticalSection((CRITICAL_SECTION *)mutex);
        delete (CRITICAL_SECTION *)mutex;
    }

    void worker_mutex_lock(void *mutex)
    {
        if (mutex)
            EnterCriticalSection((CRITICAL_SECTION *)mutex);
    }

    void worker_mutex_unlock(void *mutex)
    {
        if (mutex)
            LeaveCriticalSection((CRITICAL_SECTION *)mutex);
    }

    void worker_sleep_ms(unsigned int ms) { Sleep((DWORD)ms); }

} // namespace launcher
