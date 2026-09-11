#ifndef LAUNCHER_GFX_DOS_H
#define LAUNCHER_GFX_DOS_H

// The two hooks the DOS backend needs that are not part of the portable gfx
// contract. Included only by gfx_dos.cpp and input_dos.cpp.
//
// They exist because DOS has no window system: there is nothing to draw a
// mouse pointer, and nothing to send a close request. Both are jobs the OS
// does for the SDL and Win32 backends, so putting them in gfx.h would add
// two functions that every other backend would have to stub out for no
// reason.

namespace launcher
{
    namespace gfx
    {

        // Where present() should stamp the pointer. Pushed in by the input
        // backend once per frame; `visible` is false while the pointer is
        // off-screen or no mouse driver was found.
        void dos_set_cursor(int x, int y, bool visible);

        // Latches display_close_requested(). The input backend calls this for
        // the quit key, which is the only "close the window" DOS has.
        void dos_request_close();

        // Hand the screen back to DOS, and take it again.
        //
        // A game sets its own video mode, and a linear frame buffer left
        // mapped across a program that knows nothing about it is how a DOS
        // machine ends up needing the power switch. process_dos.cpp brackets
        // every launch with these.
        //
        // The backbuffer and every other surface survive: they are ordinary
        // memory, and only the mode and the frame buffer mapping are given
        // up. dos_resume_display() re-sets the same mode rather than
        // searching for one again, so the launcher cannot come back at a
        // different resolution than it went away at.
        void dos_suspend_display();
        bool dos_resume_display();

    } // namespace gfx
} // namespace launcher

#endif // LAUNCHER_GFX_DOS_H
