#ifndef LANCOMMANDER_TESTS_TEST_MAIN_H
#define LANCOMMANDER_TESTS_TEST_MAIN_H

#include <cstdio>
#include <string>

// A deliberately tiny harness. Catch2 or GoogleTest would contradict the SDK's
// zero-mandatory-dependencies promise, and neither builds cleanly under the
// vintage toolchains the SDK supports.

extern int g_failures;
extern int g_checks;

void report_failure(const char* file, int line, const char* expression);
void report_failure_eq(const char* file, int line, const char* expression,
                       const std::string& actual, const std::string& expected);

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
            report_failure_eq(__FILE__, __LINE__, #actual, a_, e_);      \
    } while (0)

// Each test translation unit exposes one of these.
void test_ps_quote();
void test_script_runner();
void test_script_helper();
void test_json();
void test_yaml();
void test_archive();

#endif // LANCOMMANDER_TESTS_TEST_MAIN_H
