#ifdef LAUNCHER_BACKEND_ALLEGRO
// Allegro 4's "magic main": allegro.h renames our main() to _mangled_main and
// END_OF_MAIN() below emits the real WinMain that calls it. This is Allegro's
// documented requirement on Windows, and keeping it means the transitional
// Allegro backend stays byte-for-byte the entry-point arrangement that was
// known to work on the Win9x target.
//
// (It is not what supplies `allegro_inst` — src/win/wsystem.c falls back to
// GetModuleHandle(NULL) during system init — so this is about matching the
// documented setup, not about fixing a specific fault.)
//
// allegro.h must precede windows.h: Allegro declares `struct BITMAP` while
// wingdi.h typedefs its own BITMAP. winalleg.h reconciles the two.
// Transitional, along with the rest of the Allegro backend.
#include <allegro.h>
#include <winalleg.h>
#else
#include <windows.h>
#endif

#include "app/app.h"

// Initial window size. The chrome enforces a 640x480 minimum on resize.
static const int WINDOW_W = 800;
static const int WINDOW_H = 600;

int main(int argc, char *argv[])
{
    (void)argc;
    (void)argv;

    launcher::App app;

    if (!app.init(WINDOW_W, WINDOW_H))
    {
        MessageBoxA(NULL, "Failed to initialize the launcher.",
                    "LANCommander", MB_OK | MB_ICONERROR);
        return 1;
    }

    int result = app.run();

    app.shutdown();

    return result;
}

#ifdef LAUNCHER_BACKEND_ALLEGRO
END_OF_MAIN()
#endif
