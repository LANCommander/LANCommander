#include "test_main.h"

#include "lancommander/script/picoposh_script_runner.h"
#include "lancommander/script/script_helper.h"
#include "lancommander/util/path.h"

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
}
