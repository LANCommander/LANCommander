#include "script_runner.h"

#include "logger.h"

#include <cstdio>
#include <sstream>

#include <windows.h>
#include <shellapi.h>

namespace
{
    const char* FileNameFor(Script::Type type)
    {
        switch (type)
        {
        case Script::TypeInstall:     return "Install.bat";
        case Script::TypeUninstall:   return "Uninstall.bat";
        case Script::TypeNameChange:  return "ChangeName.bat";
        case Script::TypeKeyChange:   return "ChangeKey.bat";
        case Script::TypeBeforeStart: return "BeforeStart.bat";
        case Script::TypeAfterStop:   return "AfterStop.bat";
        default:                      return NULL;
        }
    }

    bool EnsureDirs(const std::string& installDir, const std::string& ownerId)
    {
        // CreateDirectory returns FALSE if the dir already exists, which is
        // fine — GetLastError() == ERROR_ALREADY_EXISTS. We only care about
        // hard failures, and the script execution that follows will surface
        // those via its own error.
        std::string lanDir = installDir + "\\.lancommander";
        CreateDirectoryA(lanDir.c_str(), NULL);
        std::string ownerDir = lanDir + "\\" + ownerId;
        CreateDirectoryA(ownerDir.c_str(), NULL);
        return true;
    }
}

bool ScriptRunner::Save(const std::string& installDir,
                        const std::string& ownerId,
                        Script::Type type,
                        const std::string& contents,
                        std::string* errorOut)
{
    const char* fileName = FileNameFor(type);
    if (!fileName)
    {
        if (errorOut) *errorOut = "Unsupported script type";
        return false;
    }
    EnsureDirs(installDir, ownerId);

    std::string path = installDir + "\\.lancommander\\" + ownerId + "\\" +
                       fileName;
    FILE* f = fopen(path.c_str(), "wb");
    if (!f)
    {
        if (errorOut) *errorOut = "Could not open " + path + " for writing";
        return false;
    }
    size_t n = fwrite(contents.data(), 1, contents.size(), f);
    fclose(f);
    if (n != contents.size())
    {
        if (errorOut) *errorOut = "Short write to " + path;
        return false;
    }
    return true;
}

int ScriptRunner::SaveAll(const std::string& installDir,
                          const std::string& ownerId,
                          const std::vector<Script>& scripts)
{
    int written = 0;
    for (size_t i = 0; i < scripts.size(); ++i)
    {
        const Script& s = scripts[i];
        if (!FileNameFor(s.type)) continue;
        std::string err;
        if (Save(installDir, ownerId, s.type, s.contents, &err))
            ++written;
        else
            LogError("Save script (%s/%d) failed: %s",
                     ownerId.c_str(), (int)s.type, err.c_str());
    }
    return written;
}

std::string ScriptRunner::PathFor(const std::string& installDir,
                                  const std::string& ownerId,
                                  Script::Type type)
{
    const char* fileName = FileNameFor(type);
    if (!fileName) return std::string();
    return installDir + "\\.lancommander\\" + ownerId + "\\" + fileName;
}

bool ScriptRunner::Exists(const std::string& installDir,
                          const std::string& ownerId, Script::Type type)
{
    std::string path = PathFor(installDir, ownerId, type);
    if (path.empty()) return false;
    DWORD attr = GetFileAttributesA(path.c_str());
    return attr != INVALID_FILE_ATTRIBUTES &&
           !(attr & FILE_ATTRIBUTE_DIRECTORY);
}

int ScriptRunner::Run(const std::string& installDir,
                      const std::string& ownerId, Script::Type type,
                      const std::map<std::string, std::string>& vars)
{
    std::string path = PathFor(installDir, ownerId, type);
    if (path.empty()) return -1;
    DWORD attr = GetFileAttributesA(path.c_str());
    if (attr == INVALID_FILE_ATTRIBUTES) return -1;

    for (std::map<std::string, std::string>::const_iterator it = vars.begin();
         it != vars.end(); ++it)
    {
        SetEnvironmentVariableA(it->first.c_str(), it->second.c_str());
    }

    SHELLEXECUTEINFOA sei;
    ZeroMemory(&sei, sizeof(sei));
    sei.cbSize       = sizeof(sei);
    sei.fMask        = SEE_MASK_NOCLOSEPROCESS;
    sei.lpVerb       = "open";
    sei.lpFile       = path.c_str();
    sei.lpDirectory  = installDir.c_str();
    sei.nShow        = SW_SHOWNORMAL;

    if (!ShellExecuteExA(&sei) || !sei.hProcess)
    {
        LogError("ShellExecuteExA(%s) failed (GLE=%lu)",
                 path.c_str(), (unsigned long)GetLastError());
        return -1;
    }

    WaitForSingleObject(sei.hProcess, INFINITE);
    DWORD code = 0;
    if (!GetExitCodeProcess(sei.hProcess, &code)) code = (DWORD)-1;
    CloseHandle(sei.hProcess);
    return (int)code;
}
