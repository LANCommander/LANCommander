#include "test_main.h"

#include "lancommander/script/picoposh_script_runner.h"
#include "lancommander/script/script_helper.h"
#include "lancommander/util/path.h"

#include <string>
#include <utility>
#include <vector>

using namespace lancommander;

namespace {

ScriptResult run(PicoPoshScriptRunner& runner, const std::string& source,
                 const std::string& working_directory = std::string())
{
    return runner.run_inline(source, "test", working_directory, ScriptVariableList());
}

} // namespace

void test_script_runner()
{
    PicoPoshScriptRunner runner;

    // --- $Return of each shape --------------------------------------------
    {
        const ScriptResult r = run(runner, "$Return = 42\n");
        CHECK(r.success);
        CHECK(r.has_return_value);
        CHECK_EQ(r.return_value, "42");
        CHECK_EQ(r.return_json, "42");

        int value = 0;
        CHECK(script::result_to_int(r, &value));
        CHECK(value == 42);
    }

    {
        const ScriptResult r = run(runner, "$Return = $true\n");
        CHECK(r.has_return_value);
        CHECK_EQ(r.return_json, "true");
        CHECK(script::result_to_bool(r));
    }

    {
        const ScriptResult r = run(runner, "$Return = $false\n");
        // $false is a value, not the absence of one.
        CHECK(r.has_return_value);
        CHECK_EQ(r.return_json, "false");
        CHECK(!script::result_to_bool(r));
    }

    {
        const ScriptResult r =
            run(runner, "$Return = @{ Path = 'pkg.zip'; Version = '1.2' }\n");
        CHECK(r.has_return_value);
        CHECK(r.return_json.find("\"Path\":\"pkg.zip\"") != std::string::npos);

        Result<Package> package = script::result_to_package(r);
        CHECK(package.success);
        CHECK_EQ(package.value.path, "pkg.zip");
        CHECK_EQ(package.value.version, "1.2");
    }

    // --- no $Return --------------------------------------------------------
    {
        const ScriptResult r = run(runner, "Write-Output 'hello'\n");
        CHECK(r.success);
        CHECK(!r.has_return_value);
        CHECK(!script::result_to_bool(r));
        // The runner adds nothing to the script's output.
        CHECK_EQ(r.output, "hello\n");
    }

    // --- $Return survives `exit` ------------------------------------------
    //
    // Reading it back is a separate run against a session that outlives the
    // script, so unlike the old epilogue-based approach `exit` no longer
    // destroys the result.
    {
        const ScriptResult r = run(runner, "$Return = 99\nexit 7\n");
        CHECK(!r.success);
        CHECK(r.exit_code == 7);
        CHECK(r.has_return_value);
        CHECK_EQ(r.return_value, "99");
    }

    // An exit code with no $Return still reports through result_to_int.
    {
        const ScriptResult r = run(runner, "exit 3\n");
        CHECK(r.exit_code == 3);
        CHECK(!r.has_return_value);

        int value = 0;
        CHECK(script::result_to_int(r, &value));
        CHECK(value == 3);
    }

    // --- error line numbers are the script's own ---------------------------
    //
    // Nothing is prepended, so a fault on line 3 reports as line 3 no matter
    // how many variables were injected.
    {
        ScriptVariableList variables;
        variables.push_back(ScriptVariable::of_string("A", "1"));
        variables.push_back(ScriptVariable::of_string("B", "2"));
        variables.push_back(ScriptVariable::of_json("C", "{\"x\":1}"));

        const ScriptResult r = runner.run_inline(
            "$x = 1\n$y = 2\ntrap { }\n", "test.ps1", "", variables);

        CHECK(!r.success);
        CHECK(r.error.find(":3:") != std::string::npos);
    }

    // --- working directory --------------------------------------------------
    {
        const std::string before = path::get_current_directory();
        const std::string temp = path::temp_directory();

        const ScriptResult r = run(runner, "$Return = (Get-Location).Path\n", temp);
        CHECK(r.success);
        CHECK(r.return_value.size() > 0);

        // Set-Location mutates the real process CWD and picoposh never puts it
        // back, so the runner must.
        CHECK_EQ(path::get_current_directory(), before);
    }

    {
        const ScriptResult r = run(runner, "Set-Location -Path '..'\n",
                                   path::temp_directory());
        CHECK(r.success);
        CHECK(path::get_current_directory() != path::temp_directory());
    }

    {
        // A missing working directory fails cleanly without entering picoposh.
        const ScriptResult r =
            run(runner, "$Return = 1\n", path::combine(path::temp_directory(),
                                                       "lc_definitely_not_here_9f3a"));
        CHECK(!r.success);
        CHECK(r.error.find("working directory does not exist") != std::string::npos);
        CHECK(r.output.empty());
    }

    // --- object injection, end to end --------------------------------------
    {
        ScriptVariableList variables;
        variables.push_back(ScriptVariable::of_json(
            "GameManifest", "{\"Title\":\"Quake III Arena\",\"Version\":\"1.32\"}"));

        const ScriptResult r = runner.run_inline(
            "$Return = $GameManifest.Title\n", "test", "", variables);

        CHECK(r.success);
        CHECK_EQ(r.return_value, "Quake III Arena");
    }

    // --- later variables win, which is how custom fields shadow built-ins ---
    {
        ScriptVariableList variables;
        variables.push_back(ScriptVariable::of_string("ServerAddress", "first"));
        variables.push_back(ScriptVariable::of_string("ServerAddress", "second"));

        const ScriptResult r = runner.run_inline(
            "$Return = $ServerAddress\n", "test", "", variables);

        CHECK_EQ(r.return_value, "second");
    }

    // --- the streaming callback sees exactly what the script printed -------
    {
        std::string streamed;
        PicoPoshScriptRunner tapped;
        tapped.set_output_callback(
            [&streamed](int stream, const char* bytes, std::size_t len) {
                if (stream == 0)
                    streamed.append(bytes, len);
            });

        ScriptVariableList variables;
        variables.push_back(ScriptVariable::of_json("Manifest", "{\"a\":1}"));

        const ScriptResult r = tapped.run_inline(
            "Write-Output 'line one'\nWrite-Output 'line two'\n$Return = 3\n",
            "test", "", variables);

        CHECK(r.success);
        CHECK(r.has_return_value);
        CHECK_EQ(streamed, "line one\nline two\n");
    }

    // --- a runaway loop is bounded rather than hanging the caller ----------
    {
        PicoPoshScriptRunner bounded;
        bounded.set_step_limit(5000);

        const ScriptResult r = bounded.run_inline(
            "$i = 0\nwhile ($true) { $i = $i + 1 }\n", "runaway", "",
            ScriptVariableList());

        CHECK(!r.success);
        CHECK(r.error.find("step limit") != std::string::npos);
    }

    // --- run_file goes through the same injection path ---------------------
    {
        Result<std::string> temp_script =
            script::save_temp_script(std::string("$Return = $Injected\n"));
        CHECK(temp_script.success);

        if (temp_script.success) {
            ScriptVariableList variables;
            variables.push_back(ScriptVariable::of_string("Injected", "from-file"));

            const ScriptResult r = runner.run_file(temp_script.value, "", variables);

            CHECK(r.success);
            CHECK_EQ(r.return_value, "from-file");

            path::remove_file(temp_script.value);
        }
    }

    {
        const ScriptResult r = runner.run_file("does_not_exist.ps1", "",
                                               ScriptVariableList());
        CHECK(!r.success);
        CHECK(r.error.find("could not open script") != std::string::npos);
    }
    // --- debug hook --------------------------------------------------------
    {
        PicoPoshScriptRunner debugged;
        std::vector<int> lines;
        std::string name_seen;
        std::string x_at_line_3;

        debugged.set_debug_hook(
            [&](const ScriptDebugStop& stop, const IScriptDebugScope& scope) {
                lines.push_back(stop.line);
                if (name_seen.empty())
                    name_seen = stop.script_name;
                if (stop.line == 3)
                    scope.get_variable("x", &x_at_line_3);
                return ScriptDebugAction::Continue;
            });

        const ScriptResult r = debugged.run_inline(
            "$x = 1\n$x = $x + 1\nWrite-Host \"x=$x\"\n", "hooked.ps1", "",
            ScriptVariableList());

        CHECK(r.success);
        CHECK_EQ(r.output, "x=2\n");
        // One stop per statement, and the third is a pipeline -- the shape
        // that had no line number of its own until picoposh started stamping
        // one on, and the shape most Write-Host lines have.
        CHECK(lines.size() == 3);
        if (lines.size() == 3) {
            CHECK(lines[0] == 1 && lines[1] == 2 && lines[2] == 3);
        }
        CHECK_EQ(name_seen, "hooked.ps1");
        // Read before the statement runs, so $x is already 2 by line 3.
        CHECK_EQ(x_at_line_3, "2");
    }

    // The variable-injection preamble and the $Return read-back run in the
    // same session, and a breakpoint must not land in either: they are the
    // runner's bookkeeping, not anything the script author wrote.
    {
        PicoPoshScriptRunner debugged;
        std::vector<std::string> names_seen;

        debugged.set_debug_hook(
            [&](const ScriptDebugStop& stop, const IScriptDebugScope&) {
                names_seen.push_back(stop.script_name);
                return ScriptDebugAction::Continue;
            });

        ScriptVariableList variables;
        variables.push_back(ScriptVariable::of_int("Count", 3));

        const ScriptResult r = debugged.run_inline(
            "$Return = $Count\n", "only-mine.ps1", "", variables);

        CHECK(r.success);
        CHECK_EQ(r.return_value, "3");
        CHECK(names_seen.size() == 1);
        for (std::size_t i = 0; i < names_seen.size(); ++i)
            CHECK_EQ(names_seen[i], "only-mine.ps1");
    }

    // Aborting ends the run where it stood.
    {
        PicoPoshScriptRunner debugged;

        debugged.set_debug_hook(
            [](const ScriptDebugStop& stop, const IScriptDebugScope&) {
                return stop.line >= 2 ? ScriptDebugAction::Abort
                                      : ScriptDebugAction::Continue;
            });

        const ScriptResult r = debugged.run_inline(
            "Write-Host 'first'\nWrite-Host 'second'\n", "aborted.ps1", "",
            ScriptVariableList());

        CHECK(!r.success);
        CHECK_EQ(r.output, "first\n");
        CHECK(r.error.find("stopped by the debugger") != std::string::npos);
    }

    // Variable names come from the live interpreter, innermost scope first.
    {
        PicoPoshScriptRunner debugged;
        bool saw_local = false;
        bool saw_global = false;

        // Line 4 -- inside f, AFTER $inner has been assigned. The hook fires
        // BEFORE each statement, so stopping on line 3 would be too early to
        // see a variable that line 3 is what creates.
        debugged.set_debug_hook(
            [&](const ScriptDebugStop& stop, const IScriptDebugScope& scope) {
                if (stop.line != 4)
                    return ScriptDebugAction::Continue;

                const std::vector<std::string> names = scope.variable_names();
                for (std::size_t i = 0; i < names.size(); ++i) {
                    if (names[i] == "inner") saw_local = true;
                    if (names[i] == "outer") saw_global = true;
                }
                return ScriptDebugAction::Continue;
            });

        const ScriptResult r = debugged.run_inline(
            "$outer = 1\n"          // 1
            "function f {\n"        // 2
            "    $inner = 2\n"      // 3
            "    Write-Host $inner\n" // 4
            "}\n"                   // 5
            "f\n",                  // 6
            "scopes.ps1", "", ScriptVariableList());

        CHECK(r.success);
        CHECK(saw_local);
        CHECK(saw_global);
    }

    // Detaching restores an undebugged run.
    {
        PicoPoshScriptRunner debugged;
        int calls = 0;

        debugged.set_debug_hook(
            [&](const ScriptDebugStop&, const IScriptDebugScope&) {
                calls++;
                return ScriptDebugAction::Continue;
            });
        debugged.run_inline("$a = 1\n", "t", "", ScriptVariableList());
        CHECK(calls == 1);

        debugged.set_debug_hook(nullptr);
        debugged.run_inline("$a = 1\n$b = 2\n", "t", "", ScriptVariableList());
        CHECK(calls == 1);
    }
    // --- `exit <n>` is not an interpreter error ----------------------------
    //
    // The codes collide: PICO_EXIT_PARSE is 2 and PICO_EXIT_UNSUPPORTED is 3,
    // so the exit code alone cannot tell a script that deliberately exited 2
    // from one that did not parse. interpreter_error is what separates them.
    {
        const ScriptResult r = run(runner, "Write-Host 'bye'\nexit 2\n");
        CHECK(!r.success);
        CHECK(r.exit_code == 2);
        CHECK(!r.interpreter_error);
        CHECK_EQ(r.output, "bye\n");
    }

    {
        const ScriptResult r = run(runner, "exit 3\n");
        CHECK(r.exit_code == 3);
        CHECK(!r.interpreter_error);
    }

    {
        const ScriptResult r = run(runner, "$a = 1\n");
        CHECK(r.success);
        CHECK(!r.interpreter_error);
    }

    {
        // Genuinely does not parse.
        const ScriptResult r = run(runner, "if ( {\n");
        CHECK(!r.success);
        CHECK(r.interpreter_error);
    }

    {
        // Genuinely fails at runtime.
        const ScriptResult r = run(runner, "$x = 1 / 0\n");
        CHECK(!r.success);
        CHECK(r.interpreter_error);
    }
    // Depth tells a "step over" apart from a "step into": statements inside a
    // function the script called are one deeper than the call.
    {
        PicoPoshScriptRunner debugged;
        std::vector<std::pair<int, int> > stops; // line, depth

        debugged.set_debug_hook(
            [&](const ScriptDebugStop& stop, const IScriptDebugScope&) {
                stops.push_back(std::make_pair(stop.line, stop.depth));
                return ScriptDebugAction::Continue;
            });

        const ScriptResult r = debugged.run_inline(
            "function helper {\n"        // 1
            "    Write-Host 'inside'\n"  // 2
            "}\n"                        // 3
            "Write-Host 'before'\n"      // 4
            "helper\n"                   // 5
            "Write-Host 'after'\n",      // 6
            "depth.ps1", "", ScriptVariableList());

        CHECK(r.success);
        CHECK_EQ(r.output, "before\ninside\nafter\n");

        int top = 0;
        int nested = 0;
        for (std::size_t i = 0; i < stops.size(); ++i) {
            if (stops[i].second == 0) top++;
            if (stops[i].second == 1) nested++;
        }

        // Line 2 runs once, inside the call from line 5.
        CHECK(nested == 1);
        CHECK(top >= 3);

        for (std::size_t i = 0; i < stops.size(); ++i) {
            if (stops[i].first == 2)
                CHECK(stops[i].second == 1);
            if (stops[i].first == 5)
                CHECK(stops[i].second == 0);
        }
    }
}
