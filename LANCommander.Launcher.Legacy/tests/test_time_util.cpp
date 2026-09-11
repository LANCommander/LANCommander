#include "test_main.h"

#include "app/time_util.h"

using namespace launcher;

namespace
{
    // 2026-09-03T19:24:49Z, verified against the epoch by round trip below.
    const char *const SAMPLE = "2026-09-03T19:24:49Z";

    void test_iso8601()
    {
        // Round trip is the property that matters: whatever we write, we must
        // read back identically, with no dependence on the host timezone.
        const std::time_t t = iso8601_to_time_t(SAMPLE);
        CHECK(t != 0);
        CHECK_EQ(iso8601_utc_from(t), std::string(SAMPLE));

        // Known anchors.
        CHECK_INT(iso8601_to_time_t("1970-01-01T00:00:00Z"), 0);
        CHECK_INT(iso8601_to_time_t("1970-01-01T00:00:01Z"), 1);
        CHECK_INT(iso8601_to_time_t("1970-01-02T00:00:00Z"), 86400);
        CHECK_INT(iso8601_to_time_t("2000-01-01T00:00:00Z"), 946684800);
        CHECK_INT(iso8601_to_time_t("2038-01-19T03:14:07Z"), 2147483647);

        // Leap day exists in 2024 and the day after it lines up.
        {
            const std::time_t feb29 = iso8601_to_time_t("2024-02-29T00:00:00Z");
            const std::time_t mar01 = iso8601_to_time_t("2024-03-01T00:00:00Z");
            CHECK(feb29 != 0);
            CHECK_INT(mar01 - feb29, 86400);
            CHECK_EQ(iso8601_utc_from(feb29), std::string("2024-02-29T00:00:00Z"));
        }

        // 2100 is not a leap year, which a naive divisible-by-4 rule gets
        // wrong and would shift every later timestamp by a day.
        {
            const std::time_t feb28 = iso8601_to_time_t("2100-02-28T00:00:00Z");
            const std::time_t mar01 = iso8601_to_time_t("2100-03-01T00:00:00Z");
            CHECK_INT(mar01 - feb28, 86400);
        }

        // Malformed input returns 0 rather than a plausible-looking date, so
        // callers can tell "no timestamp" from "the epoch".
        CHECK_INT(iso8601_to_time_t(""), 0);
        CHECK_INT(iso8601_to_time_t("garbage"), 0);
        CHECK_INT(iso8601_to_time_t("2026-09-03"), 0);        // too short
        CHECK_INT(iso8601_to_time_t("2026-13-03T00:00:00Z"), 0); // month 13
        CHECK_INT(iso8601_to_time_t("2026-00-03T00:00:00Z"), 0);
        CHECK_INT(iso8601_to_time_t("2026-09-32T00:00:00Z"), 0);
        CHECK_INT(iso8601_to_time_t("2026-09-03T25:00:00Z"), 0);
        CHECK_INT(iso8601_to_time_t("2026-09-03T00:61:00Z"), 0);

        // Lexicographic order matches chronological order. The recent_games
        // query relies on this to sort with ORDER BY on the raw text.
        CHECK(std::string("2026-01-02T00:00:00Z") < std::string("2026-01-10T00:00:00Z"));
        CHECK(std::string("2025-12-31T23:59:59Z") < std::string("2026-01-01T00:00:00Z"));
    }

    void test_play_time()
    {
        // Mirrors GameActionBarViewModel.LoadPlayStatsAsync exactly.
        CHECK_EQ(format_play_time(0), std::string("None"));
        CHECK_EQ(format_play_time(59), std::string("None"));

        // Under an hour: whole minutes, truncated.
        CHECK_EQ(format_play_time(60), std::string("1 minutes"));
        CHECK_EQ(format_play_time(119), std::string("1 minutes"));
        CHECK_EQ(format_play_time(120), std::string("2 minutes"));
        CHECK_EQ(format_play_time(3599), std::string("59 minutes"));

        // An hour and above: hours with at most one decimal, and no
        // trailing ".0" — that is what "{0:0.#}" means.
        CHECK_EQ(format_play_time(3600), std::string("1 hours"));
        CHECK_EQ(format_play_time(5400), std::string("1.5 hours"));
        CHECK_EQ(format_play_time(7200), std::string("2 hours"));
        CHECK_EQ(format_play_time(12600), std::string("3.5 hours"));

        // Truncation, not rounding, matching .NET's default for this format
        // at the boundary we care about.
        CHECK_EQ(format_play_time(3960), std::string("1.1 hours"));

        // A long-running total does not overflow into nonsense.
        CHECK_EQ(format_play_time(360000), std::string("100 hours"));

        // Negative input cannot happen through the database (reversed rows
        // are skipped) but must not produce a garbage string if it does.
        CHECK_EQ(format_play_time(-5), std::string("None"));
    }

    void test_last_played()
    {
        const std::time_t now = iso8601_to_time_t("2026-09-03T12:00:00Z");

        // Never played.
        CHECK_EQ(format_last_played(0, now), std::string("Never"));

        // Under a minute.
        CHECK_EQ(format_last_played(now, now), std::string("Just now"));
        CHECK_EQ(format_last_played(now - 59, now), std::string("Just now"));

        // Minutes, with the singular/plural boundary that the .NET version
        // spells with two separate resource strings.
        CHECK_EQ(format_last_played(now - 60, now), std::string("1 minute ago"));
        CHECK_EQ(format_last_played(now - 120, now), std::string("2 minutes ago"));
        CHECK_EQ(format_last_played(now - 3599, now), std::string("59 minutes ago"));

        // Hours.
        CHECK_EQ(format_last_played(now - 3600, now), std::string("1 hour ago"));
        CHECK_EQ(format_last_played(now - 7200, now), std::string("2 hours ago"));
        CHECK_EQ(format_last_played(now - 86399, now), std::string("23 hours ago"));

        // Days, up to but not including a week.
        CHECK_EQ(format_last_played(now - 86400, now), std::string("1 day ago"));
        CHECK_EQ(format_last_played(now - 172800, now), std::string("2 days ago"));
        CHECK_EQ(format_last_played(now - 6 * 86400, now), std::string("6 days ago"));

        // A week or more switches to an absolute date. The exact string is
        // local-time dependent, so assert the shape rather than the value:
        // it must no longer be relative.
        {
            const std::string s = format_last_played(now - 7 * 86400, now);
            CHECK(s.find("ago") == std::string::npos);
            CHECK(s.find("2026") != std::string::npos);
        }

        // A clock that jumped backwards must not report a huge interval.
        CHECK_EQ(format_last_played(now + 10000, now), std::string("Just now"));
    }
    // The two sources of timestamps disagree about zones, so the parser has
    // to tell them apart. See the comment on iso8601_to_time_t.
    void test_iso8601_zones()
    {
        // A trailing Z is UTC, whatever the host timezone is.
        CHECK_INT(iso8601_to_time_t("2000-01-01T00:00:00Z"), 946684800);

        // Fractional seconds are accepted and ignored. /api/PlaySessions
        // returns seven decimal places.
        CHECK_INT(iso8601_to_time_t("2000-01-01T00:00:00.1234567Z"), 946684800);

        // An explicit offset counts as a zone, so it is NOT shifted again as
        // if it were a local reading.
        CHECK_INT(iso8601_to_time_t("2000-01-01T00:00:00+00:00"), 946684800);

        // A bare timestamp is local. The exact value depends on the host
        // timezone, so assert the relationship rather than a constant: it is
        // the UTC reading shifted by a whole number of minutes, and by less
        // than a day.
        {
            const std::time_t utc = iso8601_to_time_t("2000-06-15T12:00:00Z");
            const std::time_t local = iso8601_to_time_t("2000-06-15T12:00:00");

            CHECK(local != 0);

            const long diff = (long)(local - utc);
            CHECK(diff > -86400 && diff < 86400);
            CHECK_INT((int)(diff % 60), 0);
        }

        // Ordering is what the Recently Played list actually depends on, and
        // it must hold within a single source regardless of zone handling.
        {
            CHECK(iso8601_to_time_t("2026-09-04T15:42:55.4771387") >
                  iso8601_to_time_t("2026-09-03T01:12:07.2589789"));

            CHECK(iso8601_to_time_t("2026-09-04T15:42:55Z") >
                  iso8601_to_time_t("2026-09-03T01:12:07Z"));
        }

        // Garbage is still zero rather than 1970.
        CHECK_INT(iso8601_to_time_t("not a timestamp"), 0);
        CHECK_INT(iso8601_to_time_t(""), 0);
        CHECK_INT(iso8601_to_time_t("0001-01-01T00:00:00"), 0);
    }
} // namespace

void test_time_util()
{
    test_iso8601();
    test_iso8601_zones();
    test_play_time();
    test_last_played();
}
