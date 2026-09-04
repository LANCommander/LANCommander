#ifndef LAUNCHER_TESTS_TEST_MAIN_H
#define LAUNCHER_TESTS_TEST_MAIN_H

#include <cstdio>
#include <string>

// Same deliberately tiny harness as liblancommander/tests. A framework would
// pull a dependency into a tree that is otherwise buildable with vintage
// toolchains, for very little gain at this size.

extern int g_failures;
extern int g_checks;

void report_failure(const char *file, int line, const char *expression);
void report_failure_eq(const char *file, int line, const char *expression,
                       const std::string &actual, const std::string &expected);

#define CHECK(expr)                                                     \
    do {                                                                \
        ++g_checks;                                                     \
        if (!(expr))                                                    \
            report_failure(__FILE__, __LINE__, #expr);                  \
    } while (0)

#define CHECK_EQ(actual, expected)                                      \
    do {                                                                \
        ++g_checks;                                                     \
        const std::string a_ = (actual);                                \
        const std::string e_ = (expected);                              \
        if (a_ != e_)                                                   \
            report_failure_eq(__FILE__, __LINE__, #actual, a_, e_);     \
    } while (0)

#define CHECK_INT(actual, expected)                                     \
    do {                                                                \
        ++g_checks;                                                     \
        const long a_ = (long)(actual);                                 \
        const long e_ = (long)(expected);                               \
        if (a_ != e_) {                                                 \
            ++g_failures;                                               \
            std::printf("FAIL %s:%d: %s\n  actual:   %ld\n  expected: %ld\n", \
                        __FILE__, __LINE__, #actual, a_, e_);           \
        }                                                               \
    } while (0)

// Each test translation unit exposes one of these.
void test_chrome_geometry();
void test_layout();
void test_text_edit();
void test_time_util();
void test_play_sessions();
void test_library_sections();
void test_depot_sections();
void test_game_menu();
void test_text_wrap();
void test_gfx_types();

#endif // LAUNCHER_TESTS_TEST_MAIN_H
