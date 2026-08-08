#include "test_main.h"

int g_failures = 0;
int g_checks = 0;

void report_failure(const char* file, int line, const char* expression)
{
    ++g_failures;
    std::printf("FAIL %s:%d: %s\n", file, line, expression);
}

void report_failure_eq(const char* file, int line, const char* expression,
                       const std::string& actual, const std::string& expected)
{
    ++g_failures;
    std::printf("FAIL %s:%d: %s\n  actual:   \"%s\"\n  expected: \"%s\"\n",
                file, line, expression, actual.c_str(), expected.c_str());
}

int main()
{
    test_ps_quote();
    test_script_runner();
    test_script_helper();
    test_json();
    test_yaml();
    test_cmdlets();
    test_cmdlets_api();
    test_archive();

    std::printf("\n%d checks, %d failures\n", g_checks, g_failures);
    return g_failures == 0 ? 0 : 1;
}
