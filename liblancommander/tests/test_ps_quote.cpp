#include "test_main.h"

#include "lancommander/script/picoposh_script_runner.h"

using namespace lancommander;

namespace {

// Round-trips a value through a real interpreter run: inject it, have the
// script hand it straight back, compare.
//
// This used to be the SDK's most safety-critical test, because variables were
// injected by generating `$Name = '<value>'` script text and the escaping had
// to be exactly right. It is now a much weaker claim — values cross the
// picoposh session API as strings and never reach the parser — but it is worth
// keeping precisely to prove that: none of these inputs is special any more.
void round_trip(const std::string& value)
{
    PicoPoshScriptRunner runner;

    ScriptVariableList variables;
    variables.push_back(ScriptVariable::of_string("Injected", value));

    const ScriptResult result =
        runner.run_inline("$Return = $Injected\n", "round_trip", "", variables);

    CHECK(result.success);
    CHECK_EQ(result.return_value, value);
}

} // namespace

void test_ps_quote()
{
    round_trip("plain");
    round_trip("");
    round_trip("O'Brien");
    round_trip("C:\\Program Files (x86)\\Foo");

    // Under the old text-generating injector these were the dangerous cases:
    // a `$(...)` would have been evaluated, and a quote could close the
    // literal and start a new statement. Now they are just characters.
    round_trip("$(Get-ChildItem)");
    round_trip("\"$Env:Path\"");
    round_trip("'; Remove-Item C:\\ -Recurse; #");
    round_trip("`n`t backticks");

    // Newlines survive intact. The old injector had to flatten them to keep
    // one variable per generated line so error line numbers stayed accurate.
    round_trip("first\nsecond\nthird");

    // A name that could never appear as a bare $Identifier. Custom fields come
    // from the server and routinely contain spaces.
    {
        PicoPoshScriptRunner runner;

        ScriptVariableList variables;
        variables.push_back(ScriptVariable::of_string("Some Custom Field", "value"));

        const ScriptResult result = runner.run_inline(
            "$Return = (Get-Variable -Name 'Some Custom Field').Value\n",
            "spaced", "", variables);

        CHECK(result.success);
        CHECK_EQ(result.return_value, "value");
    }

    // Typed kinds reach the script as their type, not as text.
    {
        PicoPoshScriptRunner runner;

        ScriptVariableList variables;
        variables.push_back(ScriptVariable::of_int("Port", 27960));
        variables.push_back(ScriptVariable::of_bool("Enabled", true));
        variables.push_back(ScriptVariable::of_bool("Disabled", false));
        variables.push_back(ScriptVariable::of_raw("Computed", "2 + 3"));

        // Arithmetic rather than concatenation proves Port is a number.
        ScriptResult r = runner.run_inline("$Return = $Port + 1\n", "typed", "", variables);
        CHECK_EQ(r.return_value, "27961");

        r = runner.run_inline("if ($Enabled) { $Return = 'yes' } else { $Return = 'no' }\n",
                              "typed", "", variables);
        CHECK_EQ(r.return_value, "yes");

        // The one that a [bool] cast would get wrong: PowerShell casts any
        // non-empty string to $true, so 'false' must not go through [bool].
        r = runner.run_inline("if ($Disabled) { $Return = 'yes' } else { $Return = 'no' }\n",
                              "typed", "", variables);
        CHECK_EQ(r.return_value, "no");

        r = runner.run_inline("$Return = $Computed\n", "typed", "", variables);
        CHECK_EQ(r.return_value, "5");
    }
}
