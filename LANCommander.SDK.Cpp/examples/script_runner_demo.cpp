// script_runner_demo — run a .ps1 through the embedded picoposh interpreter
// and print everything the SDK extracted from it.
//
//   script_runner_demo <script.ps1> [working-directory] [Name=Value ...]
//
// Handy for eyeballing what a real LANCommander script does under picoposh
// before wiring it into a launcher.

#include "lancommander/script/picoposh_script_runner.h"

#include <cstdio>
#include <string>

using namespace lancommander;

namespace {

void stream_output(int stream, const char* bytes, std::size_t len)
{
    std::fwrite(bytes, 1, len, stream == 0 ? stdout : stderr);
}

} // namespace

int main(int argc, char** argv)
{
    if (argc < 2) {
        std::fprintf(stderr,
                     "usage: %s <script.ps1> [working-directory] [Name=Value ...]\n",
                     argv[0]);
        return 2;
    }

    const std::string script_path = argv[1];
    const std::string working_directory = (argc >= 3) ? argv[2] : "";

    ScriptVariableList variables;
    for (int i = 3; i < argc; ++i) {
        const std::string pair = argv[i];
        const std::size_t eq = pair.find('=');
        if (eq == std::string::npos) {
            std::fprintf(stderr, "ignoring '%s': expected Name=Value\n", pair.c_str());
            continue;
        }
        variables.push_back(ScriptVariable::of_string(pair.substr(0, eq),
                                                      pair.substr(eq + 1)));
    }

    PicoPoshScriptRunner runner;
    runner.set_output_callback(&stream_output);

    std::printf("--- running %s ---\n", script_path.c_str());
    const ScriptResult result =
        runner.run_file(script_path, working_directory, variables);

    std::printf("\n--- result ---\n");
    std::printf("success:        %s\n", result.success ? "yes" : "no");
    std::printf("exit code:      %d\n", result.exit_code);
    std::printf("has $Return:    %s\n", result.has_return_value ? "yes" : "no");

    if (result.has_return_value) {
        std::printf("$Return (text): %s\n", result.return_value.c_str());
        std::printf("$Return (json): %s\n", result.return_json.c_str());
    }

    if (!result.error.empty())
        std::printf("errors:\n%s", result.error.c_str());

    return result.success ? 0 : 1;
}
