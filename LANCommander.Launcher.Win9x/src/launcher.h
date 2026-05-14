#ifndef LANCOMMANDER_WIN9X_LAUNCHER_H
#define LANCOMMANDER_WIN9X_LAUNCHER_H

#include "game_client.h"
#include <string>

const ManifestAction* PickPrimaryAction(const GameManifest& manifest);

// On success, *processHandleOut receives a HANDLE (cast from void*) that the
// caller owns and must CloseHandle() once the process has been waited on.
// Pass NULL if you don't care about waiting on the process.
bool LaunchAction(const ManifestAction& action,
                  const std::string& installDir,
                  const std::string& serverAddress,
                  void** processHandleOut,
                  std::string* errorOut);

#endif
