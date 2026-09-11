// process_dos.cpp — game launching on DOS.
//
// DOS runs one program at a time, so "launch" means: give the machine to the
// game, and take it back when the game exits. Concretely —
//
//   1. Drop out of the VESA mode and back to text. The game will set its own
//      mode, and leaving a linear frame buffer mapped across a program that
//      knows nothing about it is how you get a machine that has to be
//      power-cycled.
//   2. chdir into the working directory, because DOS has one current
//      directory per machine rather than one per process.
//   3. system(), which goes through COMMAND.COM and therefore gets PATH
//      search and .BAT support for free.
//   4. Put the video mode and the working directory back.
//
// The launcher is not running while any of that happens, so there is no
// process to poll and nothing to terminate. process_running() says so.
//
// Memory is the one thing that can make this fail where the Windows path
// would not: the launcher's DPMI host and its own conventional-memory
// footprint stay resident, so a real-mode game that needs almost all of the
// first 640K may not fit. Nothing here can fix that; the error at least
// names it.

#include "app/process.h"
#include "app/logger.h"
#include "gfx/gfx_dos.h"

#include <unistd.h>

#include <cstdlib>
#include <cstring>

namespace launcher
{

    namespace
    {
        // Non-null handle for a child that has already finished. See
        // process_start().
        int s_handle_token = 0;

        bool has_metacharacter(const std::string &s)
        {
            // system() hands the string to COMMAND.COM, so these would be
            // interpreted rather than passed through. A game path containing
            // one is far more likely to be a mistake than an intent.
            return s.find_first_of("<>|") != std::string::npos;
        }
    } // namespace

    void *process_start(const std::string &path, const std::string &args,
                        const std::string &workdir, std::string *error)
    {
        if (path.empty())
        {
            if (error)
                *error = "No program to launch";
            return NULL;
        }

        if (has_metacharacter(path) || has_metacharacter(args))
        {
            if (error)
                *error = "Launch path contains a shell character (< > |)";
            return NULL;
        }

        std::string command = path;
        if (!args.empty())
            command += " " + args;

        char saved_cwd[260];
        bool restore_cwd = false;

        if (!workdir.empty() && getcwd(saved_cwd, sizeof(saved_cwd)) != NULL)
        {
            if (chdir(workdir.c_str()) == 0)
                restore_cwd = true;
        }

        log_info("Launching (DOS, synchronous): %s", command.c_str());

        // Everything between here and dos_resume_display() runs with the
        // launcher's UI gone. Logging still works: the log file is flushed
        // per line.
        gfx::dos_suspend_display();

        const int rc = std::system(command.c_str());

        const bool restored = gfx::dos_resume_display();

        if (restore_cwd)
            chdir(saved_cwd);

        if (!restored)
        {
            // The launcher is now running blind. Saying so in the log is all
            // that can be done from here; App will fail its next present().
            log_error("Could not restore the video mode after the game exited");
        }

        if (rc == -1)
        {
            if (error)
                *error = "Could not start the game: COMMAND.COM refused it, "
                         "or there was not enough free conventional memory";
            return NULL;
        }

        log_info("Game exited with status %d", rc);

        // Non-null even though the child is already gone: the caller uses the
        // handle to mean "a launch happened", and reads process_running() to
        // find out that it is over.
        return &s_handle_token;
    }

    bool process_running(void *handle)
    {
        (void)handle;
        return false;
    }

    void process_terminate(void *handle) { (void)handle; }

    void process_close(void *handle) { (void)handle; }

    bool process_concurrent() { return false; }

} // namespace launcher
