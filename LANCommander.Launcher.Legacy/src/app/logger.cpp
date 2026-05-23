#include "app/logger.h"

#include <cstdio>
#include <cstdarg>
#include <ctime>

#include <windows.h>

namespace launcher
{

    static FILE *s_log_file = NULL;

    void log_init(const char *log_dir)
    {
        if (s_log_file)
            return;

        // Ensure the log directory exists.
        CreateDirectoryA(log_dir, NULL);

        // Build a timestamped filename: launcher-YYYY-MM-DD-HHMMSS.log
        time_t now = time(NULL);
        struct tm *t = localtime(&now);

        char path[MAX_PATH];
        sprintf(path, "%s\\launcher-%04d-%02d-%02d-%02d%02d%02d.log",
                log_dir,
                t->tm_year + 1900, t->tm_mon + 1, t->tm_mday,
                t->tm_hour, t->tm_min, t->tm_sec);

        s_log_file = fopen(path, "w");
        if (s_log_file)
            log_info("Launcher started");
    }

    void log_shutdown()
    {
        if (s_log_file)
        {
            log_info("Launcher shutting down");
            fclose(s_log_file);
            s_log_file = NULL;
        }
    }

    static void log_write(const char *level, const char *fmt, va_list args)
    {
        if (!s_log_file)
            return;

        // Timestamp
        time_t now = time(NULL);
        struct tm *t = localtime(&now);
        fprintf(s_log_file, "%04d-%02d-%02d %02d:%02d:%02d [%s] ",
                t->tm_year + 1900, t->tm_mon + 1, t->tm_mday,
                t->tm_hour, t->tm_min, t->tm_sec, level);

        vfprintf(s_log_file, fmt, args);
        fprintf(s_log_file, "\n");
        fflush(s_log_file);
    }

    void log_info(const char *fmt, ...)
    {
        va_list args;
        va_start(args, fmt);
        log_write("INFO", fmt, args);
        va_end(args);
    }

    void log_warn(const char *fmt, ...)
    {
        va_list args;
        va_start(args, fmt);
        log_write("WARN", fmt, args);
        va_end(args);
    }

    void log_error(const char *fmt, ...)
    {
        va_list args;
        va_start(args, fmt);
        log_write("ERROR", fmt, args);
        va_end(args);
    }

} // namespace launcher
