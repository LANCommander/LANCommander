#ifndef LANCOMMANDER_WIN9X_LOGGER_H
#define LANCOMMANDER_WIN9X_LOGGER_H

// printf-style append to errors.log next to the exe, prefixed with a local
// timestamp. Failures are swallowed.
void LogError(const char* fmt, ...);

#endif
