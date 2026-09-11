#ifndef LAUNCHER_APP_PROCESS_H
#define LAUNCHER_APP_PROCESS_H

#include <string>

// Launching a game, and knowing when it stopped.
//
// The two platforms disagree about something fundamental here, and the seam
// is shaped around the disagreement rather than hiding it:
//
//   Windows — the game runs alongside the launcher. process_start() returns
//   as soon as it has started, process_running() polls, and Stop terminates.
//
//   DOS — the game *is* the machine for as long as it runs. There is no
//   multitasking, so process_start() does not return until the game has
//   exited, and process_running() is false the moment it does. Stop can
//   never be pressed, because the launcher is not drawing while the game is
//   up. The launcher drops out of its video mode before handing the machine
//   over and restores it afterwards.
//
// Everything above process_concurrent() is the same on both, which is what
// lets the play-session bookkeeping in screen_game_detail stay one path.

namespace launcher
{

    // Starts `path` with `args` in `workdir`. Returns an opaque handle, or
    // NULL with `error` filled in. `args` is a single command-line string,
    // matching what the server's Action model carries.
    void *process_start(const std::string &path, const std::string &args,
                        const std::string &workdir, std::string *error);

    // True while the child is still running. Always false on DOS: by the
    // time anything can ask, the child has exited.
    bool process_running(void *handle);

    // Kills the child. A no-op where it has necessarily already exited.
    void process_terminate(void *handle);

    // Releases the handle. Does not stop the child.
    void process_close(void *handle);

    // True where a started process runs alongside the launcher. Callers use
    // it for what they show the user, not for control flow — the polling
    // above is correct either way.
    bool process_concurrent();

} // namespace launcher

#endif // LAUNCHER_APP_PROCESS_H
