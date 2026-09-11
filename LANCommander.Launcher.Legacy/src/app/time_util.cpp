#include "app/time_util.h"

#include <cstdio>
#include <cstring>

namespace launcher
{

    namespace
    {
        // timegm() is not portable and _mkgmtime() is MSVC/MinGW-only, so the
        // conversion is done by hand. Days since the epoch from a civil date,
        // by Howard Hinnant's algorithm: exact for all years in range, and no
        // dependence on the host timezone, which is the whole point.
        long long days_from_civil(int y, int m, int d)
        {
            y -= (m <= 2);
            const long long era = (y >= 0 ? y : y - 399) / 400;
            const unsigned yoe = static_cast<unsigned>(y - era * 400);
            const unsigned doy = (153u * (m + (m > 2 ? -3 : 9)) + 2u) / 5u + d - 1;
            const unsigned doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
            return era * 146097LL + static_cast<long long>(doe) - 719468LL;
        }
    } // namespace

    std::string iso8601_utc_from(std::time_t t)
    {
        std::tm g;
        std::memset(&g, 0, sizeof(g));

#ifdef _WIN32
        if (gmtime_s(&g, &t) != 0)
            return std::string();
#else
        if (!gmtime_r(&t, &g))
            return std::string();
#endif

        char buf[32];
        std::sprintf(buf, "%04d-%02d-%02dT%02d:%02d:%02dZ",
                     g.tm_year + 1900, g.tm_mon + 1, g.tm_mday,
                     g.tm_hour, g.tm_min, g.tm_sec);
        return buf;
    }

    std::string iso8601_utc_now()
    {
        return iso8601_utc_from(std::time(NULL));
    }

    namespace
    {
        // Seconds to ADD to a local wall-clock reading to get UTC, worked out
        // by round-tripping a known instant through the C library rather than
        // by reading a global. timegm() is not portable and _timezone is not
        // on every toolchain this builds with.
        //
        // Sampled at the timestamp being converted, so a summer date gets the
        // summer offset: a fixed "current offset" would put every session
        // recorded on the other side of a DST change an hour out.
        long local_utc_offset(std::time_t utc_guess)
        {
            std::tm local_tm;
            std::tm utc_tm;

            {
                const std::tm *p = std::localtime(&utc_guess);
                if (!p) return 0;
                local_tm = *p;
            }
            {
                const std::tm *p = std::gmtime(&utc_guess);
                if (!p) return 0;
                utc_tm = *p;
            }

            const long long local_secs =
                days_from_civil(local_tm.tm_year + 1900, local_tm.tm_mon + 1,
                                local_tm.tm_mday) * 86400LL +
                local_tm.tm_hour * 3600LL + local_tm.tm_min * 60LL + local_tm.tm_sec;

            const long long utc_secs =
                days_from_civil(utc_tm.tm_year + 1900, utc_tm.tm_mon + 1,
                                utc_tm.tm_mday) * 86400LL +
                utc_tm.tm_hour * 3600LL + utc_tm.tm_min * 60LL + utc_tm.tm_sec;

            return (long)(utc_secs - local_secs);
        }

        // True when the string carries a zone: a trailing Z, or a +hh:mm /
        // -hh:mm offset after the time. Anything else is a bare local reading.
        bool has_zone(const std::string &s)
        {
            for (size_t i = s.size(); i-- > 0;)
            {
                const char c = s[i];
                if (c == 'Z' || c == 'z')
                    return true;
                if (c == '+')
                    return true;
                // A '-' only counts as an offset sign after the 'T'; the date
                // separators are dashes too.
                if (c == '-')
                    return s.find('T') != std::string::npos && i > s.find('T');
                if (c == ':' || (c >= '0' && c <= '9') || c == '.')
                    continue;
                return false;
            }
            return false;
        }
    } // namespace

    std::time_t iso8601_to_time_t(const std::string &s)
    {
        if (s.size() < 19)
            return 0;

        int year = 0, mon = 0, day = 0, hour = 0, min = 0, sec = 0;
        if (std::sscanf(s.c_str(), "%4d-%2d-%2dT%2d:%2d:%2d",
                        &year, &mon, &day, &hour, &min, &sec) != 6)
            return 0;

        if (mon < 1 || mon > 12 || day < 1 || day > 31 ||
            hour < 0 || hour > 23 || min < 0 || min > 59 || sec < 0 || sec > 60)
            return 0;

        // A year outside this window is not a timestamp anyone recorded, and
        // treating it as one is actively harmful: .NET serialises an unset
        // DateTime as "0001-01-01T00:00:00", which the server does emit --
        // /api/PlaySessions returns it for CreatedOn on some rows. Parsed
        // literally that is an epoch near -62135596800, which does not fit in
        // a 32-bit long and truncates to an arbitrary value that can come out
        // POSITIVE, sailing past a caller's "is this a real time" check and
        // sorting to the top of a most-recent-first list.
        if (year < 1970 || year > 2200)
            return 0;

        const long long days = days_from_civil(year, mon, day);
        const long long wall = days * 86400LL + hour * 3600LL + min * 60LL + sec;

        if (has_zone(s))
            return static_cast<std::time_t>(wall);

        // Bare timestamp: read as local. The offset is sampled at the instant
        // itself, using the wall reading as a good-enough seed -- it is at
        // most one UTC offset away from the answer, which only matters within
        // hours of a DST boundary.
        return static_cast<std::time_t>(
            wall + local_utc_offset(static_cast<std::time_t>(wall)));
    }

    std::string format_play_time(long total_seconds)
    {
        if (total_seconds < 60)
            return "None";

        if (total_seconds < 3600)
        {
            char buf[32];
            std::sprintf(buf, "%ld minutes", total_seconds / 60);
            return buf;
        }

        // "{0:0.#}" — one decimal place, but no trailing ".0".
        const long tenths = (total_seconds * 10) / 3600;
        const long whole = tenths / 10;
        const long frac = tenths % 10;

        char buf[32];
        if (frac == 0)
            std::sprintf(buf, "%ld hours", whole);
        else
            std::sprintf(buf, "%ld.%ld hours", whole, frac);
        return buf;
    }

    std::string format_last_played(std::time_t end, std::time_t now)
    {
        if (end == 0)
            return "Never";

        // A session that ended "in the future" means the clock moved
        // backwards. Reporting a huge negative interval helps nobody.
        const long long elapsed = (now > end) ? static_cast<long long>(now - end) : 0;

        if (elapsed < 60)
            return "Just now";

        char buf[64];

        if (elapsed < 3600)
        {
            const long n = static_cast<long>(elapsed / 60);
            std::sprintf(buf, "%ld minute%s ago", n, n == 1 ? "" : "s");
            return buf;
        }

        if (elapsed < 86400)
        {
            const long n = static_cast<long>(elapsed / 3600);
            std::sprintf(buf, "%ld hour%s ago", n, n == 1 ? "" : "s");
            return buf;
        }

        if (elapsed < 7 * 86400)
        {
            const long n = static_cast<long>(elapsed / 86400);
            std::sprintf(buf, "%ld day%s ago", n, n == 1 ? "" : "s");
            return buf;
        }

        // Older than a week: an absolute date, in local time because that is
        // the form a person reads as a calendar day.
        std::tm lt;
        std::memset(&lt, 0, sizeof(lt));

#ifdef _WIN32
        if (localtime_s(&lt, &end) != 0)
            return "Never";
#else
        if (!localtime_r(&end, &lt))
            return "Never";
#endif

        static const char *const MONTHS[] = {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun",
            "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        };

        std::sprintf(buf, "%s %d, %d",
                     MONTHS[lt.tm_mon], lt.tm_mday, lt.tm_year + 1900);
        return buf;
    }

} // namespace launcher
