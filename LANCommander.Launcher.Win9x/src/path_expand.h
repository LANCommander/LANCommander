#ifndef LANCOMMANDER_WIN9X_PATH_EXPAND_H
#define LANCOMMANDER_WIN9X_PATH_EXPAND_H

#include <string>

// Replaces tokens like %MyDocuments%, %Desktop%, %AppData% in `in` with the
// resolved special-folder path. Tokens that can't be resolved on the current
// OS are left untouched. Win9x with IE 4.0+ supports the per-user folders;
// stock Win95 (without IE 4) returns nothing here, which is the right
// fallback — let the standard %ENV% pass handle whatever it can.
std::string ExpandSpecialFolders(const std::string& in);

#endif
