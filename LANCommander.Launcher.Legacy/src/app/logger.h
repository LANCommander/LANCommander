#ifndef LAUNCHER_LOGGER_H
#define LAUNCHER_LOGGER_H

namespace launcher
{

    // Simple file logger. Call log_init() once at startup.
    // Creates a new timestamped log file per session under the
    // given directory (e.g. "Data/Logs/launcher-2026-05-23-143052.log").
    void log_init(const char *log_dir);
    void log_shutdown();

    void log_info(const char *fmt, ...);
    void log_warn(const char *fmt, ...);
    void log_error(const char *fmt, ...);

} // namespace launcher

#endif // LAUNCHER_LOGGER_H
