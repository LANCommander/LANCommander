#ifndef LAUNCHER_APP_PATHS_H
#define LAUNCHER_APP_PATHS_H

#include <string>

// Where the launcher's own files live.
//
// Everything used to be spelled relative to the CURRENT WORKING DIRECTORY:
// "assets/fonts/Inter-Regular.ttf", "Data\\Settings.yml". That only works when
// the process happens to start in the directory the executable sits in, which
// is true for a double-click from Explorer and false for a Start Menu or
// desktop shortcut with no "Start in" set, for a launch from another program,
// or for running it from a shell somewhere else.
//
// When it was false the launcher came up with no font at all — so no text
// anywhere — and wrote Settings.yml, the game database and the media cache
// into whatever directory it happened to inherit, which read to the user as
// having been logged out and having lost their library.
//
// Both now resolve against the executable's own directory, so the launcher is
// a portable folder you can start from anywhere.

namespace launcher
{

    // Directory containing the running executable, with a trailing separator.
    // Falls back to "" (and therefore to cwd-relative behaviour) if the
    // platform cannot answer, which is better than refusing to start.
    const std::string &exe_dir();

    // exe_dir() + `relative`. Accepts forward or backward slashes.
    std::string app_path(const char *relative);

} // namespace launcher

#endif // LAUNCHER_APP_PATHS_H
