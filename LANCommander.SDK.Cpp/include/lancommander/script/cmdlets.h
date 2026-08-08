#ifndef LANCOMMANDER_SCRIPT_CMDLETS_H
#define LANCOMMANDER_SCRIPT_CMDLETS_H

#include <string>
#include <vector>

#include "../types.h"

namespace lancommander {
namespace cmdlets {

// LANCommander's cmdlet pack for the embedded picoposh interpreter — the C++
// counterpart of the cmdlets the .NET SDK registers into its runspace, so a
// script written for one launcher behaves the same under the other.
//
// Registration is process-global and shared by every interpreter, matching
// picoposh's own contract. Register once at startup, before running anything:
//
//     lancommander::cmdlets::register_all();
//
// This is not automatic. A host that wants the plain picoposh language and
// nothing else simply does not call it.
//
// Not every .NET cmdlet is here. The ones that need subsystems this SDK does
// not have — SteamCMD orchestration, the Steam Web API, image manipulation —
// are absent rather than stubbed, so a script calling one gets picoposh's
// ordinary "not recognized as a cmdlet" error instead of a silent no-op.
// docs/API_REFERENCE.md lists exactly which.

// Registers every cmdlet in the pack. Safe to call more than once —
// registering a name that already exists replaces it.
Result<bool> register_all();

// Drops every host registration, including any another component made:
// picoposh's unregister is all-or-nothing.
void unregister_all();

// The names register_all() installs, in registration order. Useful for
// diagnostics and for the docs test that keeps this list honest.
std::vector<std::string> names();

} // namespace cmdlets
} // namespace lancommander

#endif // LANCOMMANDER_SCRIPT_CMDLETS_H
