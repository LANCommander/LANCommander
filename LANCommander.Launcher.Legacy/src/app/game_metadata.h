#ifndef LAUNCHER_APP_GAME_METADATA_H
#define LAUNCHER_APP_GAME_METADATA_H

#include <string>

// The small files the .NET SDK keeps beside a game's manifest, and the path
// tokens its save paths and actions are written in.
//
//   <install dir>/.lancommander/<game id>/PlayerAlias
//   <install dir>/.lancommander/<game id>/Key
//
// Both are plain text with no trailing newline, read by GameClient's
// GetPlayerAlias / GetCurrentKey. They are not incidental caches: $PlayerAlias
// and $AllocatedKey are injected from them, and the key file in particular is
// AUTHORITATIVE over the server -- see game_key() below.

namespace launcher
{

    // --- player alias --------------------------------------------------------

    // "" when the game has never had one recorded.
    std::string game_player_alias(const std::string &install_dir,
                                  const std::string &game_id);

    bool set_game_player_alias(const std::string &install_dir,
                               const std::string &game_id,
                               const std::string &alias);

    // --- key -----------------------------------------------------------------

    // The key this install is using, or "".
    //
    // The local file wins over anything the server would hand out. That is
    // deliberate upstream behaviour, not laziness: allocation is keyed partly
    // on the machine's MAC address, which is not stable between launches on
    // every adapter, so asking the server on every run hands the game a
    // different key each time and burns the pool. A key is requested only when
    // this install has none tracked.
    std::string game_key(const std::string &install_dir,
                         const std::string &game_id);

    bool set_game_key(const std::string &install_dir, const std::string &game_id,
                      const std::string &key);

    // --- path tokens ---------------------------------------------------------

    // "{InstallDir}/saves" -> "C:\Games\Thing\saves".
    //
    // Expands the {InstallDir} token, then %ENVIRONMENT% variables, then
    // normalises separators for this platform, mirroring the .NET SDK's
    // StringExtensions.ExpandEnvironmentVariables. Trailing separators are
    // trimmed so two spellings of the same directory compare equal.
    std::string expand_game_path(const std::string &path,
                                 const std::string &install_dir);

    // The inverse, for writing a path INTO a save archive: an archive packed
    // on one machine has to restore on another, where the install directory
    // and the user profile are somewhere else.
    std::string deflate_game_path(const std::string &path,
                                  const std::string &install_dir);

} // namespace launcher

#endif // LAUNCHER_APP_GAME_METADATA_H
