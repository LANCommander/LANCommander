#include "lancommander/script/batch_script_runner.h"

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <sstream>
#include <vector>

namespace lancommander {

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

ScriptResult BatchScriptRunner::run_file(
    const std::string& script_path,
    const std::string& working_directory,
    const std::map<std::string, std::string>& variables)
{
#ifdef _WIN32
    // Quote the path in case it contains spaces.
    std::string command = "\"" + script_path + "\"";
    std::string env = build_environment_block(variables);
    return execute(command, working_directory, env);
#else
    ScriptResult r;
    r.success = false;
    r.exit_code = -1;
    r.error = "BatchScriptRunner is only supported on Windows";
    return r;
#endif
}

ScriptResult BatchScriptRunner::run_inline(
    const std::string& script_contents,
    const std::string& working_directory,
    const std::map<std::string, std::string>& variables)
{
#ifdef _WIN32
    // Write contents to a temporary .bat file.
    char temp_dir[MAX_PATH + 1];
    GetTempPathA(MAX_PATH, temp_dir);

    char temp_file[MAX_PATH + 1];
    GetTempFileNameA(temp_dir, "lcs", 0, temp_file);

    // GetTempFileName creates the file — rename to .bat
    std::string bat_path = std::string(temp_file) + ".bat";
    MoveFileA(temp_file, bat_path.c_str());

    {
        std::ofstream f(bat_path.c_str(), std::ios::binary);
        if (!f.is_open()) {
            ScriptResult r;
            r.success = false;
            r.exit_code = -1;
            r.error = "Could not create temporary batch file";
            return r;
        }
        f.write(script_contents.data(), script_contents.size());
    }

    std::string env = build_environment_block(variables);
    ScriptResult result = execute("\"" + bat_path + "\"", working_directory, env);

    DeleteFileA(bat_path.c_str());
    return result;
#else
    ScriptResult r;
    r.success = false;
    r.exit_code = -1;
    r.error = "BatchScriptRunner is only supported on Windows";
    return r;
#endif
}

// ---------------------------------------------------------------------------
// Private helpers (Windows only)
// ---------------------------------------------------------------------------

#ifdef _WIN32

std::string BatchScriptRunner::build_environment_block(
    const std::map<std::string, std::string>& variables)
{
    // Start with the current environment and overlay our variables.
    // The environment block is a sequence of "KEY=VALUE\0" strings
    // terminated by an additional \0.

    std::map<std::string, std::string> env;

    // Copy current environment.
    char* current_env = GetEnvironmentStringsA();
    if (current_env) {
        const char* p = current_env;
        while (*p) {
            std::string entry(p);
            size_t eq = entry.find('=');
            if (eq != std::string::npos && eq > 0)
                env[entry.substr(0, eq)] = entry.substr(eq + 1);
            p += entry.size() + 1;
        }
        FreeEnvironmentStringsA(current_env);
    }

    // Overlay user variables.
    for (std::map<std::string, std::string>::const_iterator it = variables.begin();
         it != variables.end(); ++it) {
        env[it->first] = it->second;
    }

    // Build the block.
    std::string block;
    for (std::map<std::string, std::string>::const_iterator it = env.begin();
         it != env.end(); ++it) {
        block += it->first;
        block += '=';
        block += it->second;
        block += '\0';
    }
    block += '\0'; // Double-null terminator.
    return block;
}

ScriptResult BatchScriptRunner::execute(
    const std::string& command,
    const std::string& working_directory,
    const std::string& env_block)
{
    ScriptResult result;
    result.success = false;
    result.exit_code = -1;

    // Build the full command line: cmd.exe /C <command>
    std::string cmd_line = "cmd.exe /C " + command;

    // Create pipes for stdout capture.
    SECURITY_ATTRIBUTES sa;
    sa.nLength = sizeof(sa);
    sa.lpSecurityDescriptor = NULL;
    sa.bInheritHandle = TRUE;

    HANDLE read_pipe = NULL;
    HANDLE write_pipe = NULL;

    if (!CreatePipe(&read_pipe, &write_pipe, &sa, 0)) {
        result.error = "Failed to create pipe for stdout capture";
        return result;
    }

    // Ensure the read end is not inherited.
    SetHandleInformation(read_pipe, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOA si;
    memset(&si, 0, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdOutput = write_pipe;
    si.hStdError = write_pipe;
    si.hStdInput = NULL;

    PROCESS_INFORMATION pi;
    memset(&pi, 0, sizeof(pi));

    const char* work_dir = working_directory.empty() ? NULL : working_directory.c_str();

    // cmd_line must be mutable for CreateProcessA.
    std::vector<char> cmd_buf(cmd_line.begin(), cmd_line.end());
    cmd_buf.push_back('\0');

    BOOL ok = CreateProcessA(
        (LPCSTR)NULL,
        &cmd_buf[0],
        (LPSECURITY_ATTRIBUTES)NULL,
        (LPSECURITY_ATTRIBUTES)NULL,
        TRUE,  // inherit handles
        0,     // creation flags — no special flags for Win9x compat
        (LPVOID)const_cast<char*>(env_block.data()),
        work_dir,
        &si,
        &pi
    );

    // Close the write end in the parent so ReadFile will eventually EOF.
    CloseHandle(write_pipe);

    if (!ok) {
        CloseHandle(read_pipe);
        std::ostringstream e;
        e << "CreateProcess failed (error " << GetLastError() << ")";
        result.error = e.str();
        return result;
    }

    // Read stdout/stderr.
    std::string output;
    char buf[4096];
    DWORD bytes_read;
    while (ReadFile(read_pipe, buf, sizeof(buf), &bytes_read, NULL) && bytes_read > 0) {
        output.append(buf, bytes_read);
    }
    CloseHandle(read_pipe);

    // Wait for the process to finish.
    WaitForSingleObject(pi.hProcess, INFINITE);

    DWORD exit_code = 0;
    GetExitCodeProcess(pi.hProcess, &exit_code);

    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    result.success = (exit_code == 0);
    result.exit_code = static_cast<int>(exit_code);
    result.output = output;
    return result;
}

#else

// Stubs for non-Windows — these won't be called because run_file/run_inline
// return early, but they satisfy the linker.
std::string BatchScriptRunner::build_environment_block(
    const std::map<std::string, std::string>&)
{
    return std::string();
}

ScriptResult BatchScriptRunner::execute(
    const std::string&, const std::string&, const std::string&)
{
    ScriptResult r;
    r.success = false;
    r.exit_code = -1;
    r.error = "Not supported on this platform";
    return r;
}

#endif // _WIN32

} // namespace lancommander
