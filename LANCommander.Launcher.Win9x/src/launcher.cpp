#include "launcher.h"
#include "path_expand.h"

#include <windows.h>
#include <shellapi.h>
#include <cstring>

namespace
{
    void ReplaceAll(std::string& s, const std::string& find, const std::string& with)
    {
        if (find.empty()) return;
        size_t pos = 0;
        while ((pos = s.find(find, pos)) != std::string::npos)
        {
            s.replace(pos, find.size(), with);
            pos += with.size();
        }
    }

    void NormalizeSlashes(std::string& s)
    {
        for (size_t i = 0; i < s.size(); ++i)
            if (s[i] == '/') s[i] = '\\';
    }

    std::string ExpandEnv(const std::string& in)
    {
        std::string s = ExpandSpecialFolders(in);
        if (s.find('%') == std::string::npos) return s;
        char buf[2048];
        DWORD n = ExpandEnvironmentStringsA(s.c_str(), buf, sizeof(buf));
        if (n == 0 || n > sizeof(buf)) return s;
        return std::string(buf, n - 1);
    }

    std::string Expand(const std::string& input,
                       const std::string& installDir,
                       const std::string& serverAddress,
                       const std::map<std::string, std::string>& actionVars,
                       bool skipSlashes)
    {
        if (input.empty()) return input;
        std::string s = input;

        ReplaceAll(s, "{InstallDir}",    installDir);
        ReplaceAll(s, "{ServerAddress}", serverAddress);
        for (std::map<std::string, std::string>::const_iterator it = actionVars.begin();
             it != actionVars.end(); ++it)
        {
            ReplaceAll(s, "{" + it->first + "}", it->second);
        }

        s = ExpandEnv(s);
        if (!skipSlashes) NormalizeSlashes(s);
        return s;
    }

    bool IsAbsolute(const std::string& p)
    {
        if (p.size() >= 2 && p[1] == ':') return true;
        if (!p.empty() && (p[0] == '\\' || p[0] == '/')) return true;
        return false;
    }

    std::string JoinDir(const std::string& dir, const std::string& rel)
    {
        if (dir.empty()) return rel;
        std::string out = dir;
        if (out[out.size()-1] != '\\' && out[out.size()-1] != '/')
            out += '\\';
        out += rel;
        return out;
    }
}

const ManifestAction* PickPrimaryAction(const GameManifest& manifest)
{
    const ManifestAction* best = NULL;
    for (size_t i = 0; i < manifest.actions.size(); ++i)
    {
        const ManifestAction& a = manifest.actions[i];
        if (!a.isPrimary) continue;
        if (!best || a.sortOrder < best->sortOrder) best = &a;
    }
    if (best) return best;

    for (size_t i = 0; i < manifest.actions.size(); ++i)
    {
        const ManifestAction& a = manifest.actions[i];
        if (!best || a.sortOrder < best->sortOrder) best = &a;
    }
    return best;
}

bool LaunchAction(const ManifestAction& action,
                  const std::string& installDir,
                  const std::string& serverAddress,
                  void** processHandleOut,
                  std::string* errorOut)
{
    if (processHandleOut) *processHandleOut = NULL;

    std::string path = Expand(action.path,             installDir, serverAddress, action.variables, false);
    std::string args = Expand(action.arguments,        installDir, serverAddress, action.variables, true);
    std::string cwd  = Expand(action.workingDirectory, installDir, serverAddress, action.variables, false);

    if (path.empty())
    {
        if (errorOut) *errorOut = "Action has no path";
        return false;
    }

    if (!IsAbsolute(path)) path = JoinDir(installDir, path);
    if (cwd.empty())       cwd = installDir;
    else if (!IsAbsolute(cwd)) cwd = JoinDir(installDir, cwd);

    SHELLEXECUTEINFOA info;
    memset(&info, 0, sizeof(info));
    info.cbSize       = sizeof(info);
    info.fMask        = SEE_MASK_FLAG_NO_UI |
                        (processHandleOut ? SEE_MASK_NOCLOSEPROCESS : 0);
    info.lpVerb       = "open";
    info.lpFile       = path.c_str();
    info.lpParameters = args.empty() ? NULL : args.c_str();
    info.lpDirectory  = cwd.empty()  ? NULL : cwd.c_str();
    info.nShow        = SW_SHOWNORMAL;

    if (!ShellExecuteExA(&info))
    {
        if (errorOut)
        {
            char buf[128];
            wsprintfA(buf, "ShellExecute failed (error %lu)", GetLastError());
            *errorOut = buf;
        }
        return false;
    }

    if (processHandleOut) *processHandleOut = info.hProcess;
    return true;
}
