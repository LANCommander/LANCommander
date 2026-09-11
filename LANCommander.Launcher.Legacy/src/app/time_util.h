#ifndef LAUNCHER_APP_TIME_UTIL_H
#define LAUNCHER_APP_TIME_UTIL_H

#include <ctime>
#include <string>

// Timestamps and the human-readable strings derived from them.
//
// Play sessions are stored as ISO-8601 UTC because the numbers are subtracted
// from each other and compared against "now" — a local-time string with no
// offset silently changes meaning twice a year, and a play session that spans
// a DST boundary would come out an hour wrong.
//
// (The existing InstalledOn column in game_database.cpp does use local time
// with no offset. It is only ever displayed, never subtracted, so it is left
// alone rather than migrated; the inconsistency is noted here so the next
// person does not assume the two formats match.)
//
// Pure: no SDK, no Win32, no sqlite. Compiled into launcher_tests.

namespace launcher
{

    // "2026-09-03T19:24:49Z"
    std::string iso8601_utc_now();
    std::string iso8601_utc_from(std::time_t t);

    // Parses the format above. Returns 0 on anything it does not understand,
    // which callers treat as "no timestamp" rather than 1970.
    //
    // Zone-aware, because the two sources of timestamps disagree:
    //
    //   * The local play-session table writes a trailing "Z" and really is
    //     UTC, as the note above describes.
    //   * /api/PlaySessions returns Start/End with no zone at all. The rows
    //     the server writes today are LOCAL time -- verified against a live
    //     server, where a session recorded at 15:42 local came back as
    //     15:42 with the machine five hours behind UTC.
    //
    // So a trailing "Z" (or an explicit +hh:mm / -hh:mm offset) is honoured,
    // and a bare timestamp is read as local time. Fractional seconds are
    // accepted and ignored.
    //
    // Years outside 1970..2200 are rejected as unparseable, which is how a
    // .NET default DateTime ("0001-01-01T00:00:00") is kept out of the data.
    //
    // The one thing this cannot fix: rows written by OLDER server versions
    // stored Start/End in UTC without a "Z", so they are now read an offset
    // out. They are months or years old, which is far outside the resolution
    // anything here cares about -- "recently played" and "N days ago" are
    // unaffected.
    std::time_t iso8601_to_time_t(const std::string &s);

    // Total play time, worded exactly as the Avalonia launcher words it:
    // under a minute is "None", under an hour is whole minutes, and above
    // that is hours with at most one decimal.
    std::string format_play_time(long total_seconds);

    // Relative "last played", again matching Avalonia: "Never", "Just now",
    // "N minute(s) ago", "N hour(s) ago", "N day(s) ago" up to a week, then an
    // absolute date. Singular and plural are distinct strings there, so they
    // are here too.
    //
    // `end` and `now` are both UTC. The absolute form is rendered in local
    // time, since that is the one a person reads as a date.
    std::string format_last_played(std::time_t end, std::time_t now);

} // namespace launcher

#endif // LAUNCHER_APP_TIME_UTIL_H
