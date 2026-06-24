#include "save_sync.h"
#include "archive.h"
#include "path_expand.h"

#include <windows.h>

#include <cstdio>
#include <map>

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

    std::string ExpandEnvAndInstall(const std::string& in, const std::string& installDir)
    {
        if (in.empty()) return in;
        std::string s = in;
        ReplaceAll(s, "{InstallDir}", installDir);

        s = ExpandSpecialFolders(s);

        if (s.find('%') != std::string::npos)
        {
            char buf[2048];
            DWORD n = ExpandEnvironmentStringsA(s.c_str(), buf, sizeof(buf));
            if (n > 0 && n <= sizeof(buf)) s.assign(buf, n - 1);
        }

        for (size_t i = 0; i < s.size(); ++i)
            if (s[i] == '/') s[i] = '\\';
        return s;
    }

    std::string JoinDir(const std::string& dir, const std::string& rel)
    {
        if (rel.empty()) return dir;
        if (rel.size() >= 2 && rel[1] == ':') return rel;
        if (!rel.empty() && (rel[0] == '\\' || rel[0] == '/')) return rel;
        if (dir.empty()) return rel;
        std::string out = dir;
        if (out[out.size()-1] != '\\' && out[out.size()-1] != '/')
            out += '\\';
        out += rel;
        return out;
    }

    void TrimTrailingSlash(std::string& s)
    {
        while (!s.empty() && (s[s.size()-1] == '\\' || s[s.size()-1] == '/'))
            s.erase(s.size() - 1);
    }

    bool IsDirectory(const std::string& path)
    {
        DWORD a = GetFileAttributesA(path.c_str());
        return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY);
    }

    bool IsFile(const std::string& path)
    {
        DWORD a = GetFileAttributesA(path.c_str());
        return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
    }

    // Recursively walk `root`, adding files to the writer under
    // `archivePrefix` + relative-path-from-root.
    bool AddFolder(ZipWriter& writer, const std::string& root,
                   const std::string& archivePrefix,
                   const std::string& subdir,
                   std::string* errorOut)
    {
        std::string scanDir = subdir.empty() ? root : JoinDir(root, subdir);
        std::string pattern = JoinDir(scanDir, "*");

        WIN32_FIND_DATAA find;
        HANDLE h = FindFirstFileA(pattern.c_str(), &find);
        if (h == INVALID_HANDLE_VALUE) return true;

        do
        {
            const char* name = find.cFileName;
            if (strcmp(name, ".") == 0 || strcmp(name, "..") == 0) continue;

            std::string rel = subdir.empty() ? std::string(name)
                                             : (subdir + "\\" + name);
            std::string full = JoinDir(root, rel);

            if (find.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
            {
                if (!AddFolder(writer, root, archivePrefix, rel, errorOut))
                {
                    FindClose(h);
                    return false;
                }
            }
            else
            {
                std::string archivePath = archivePrefix;
                for (size_t i = 0; i < rel.size(); ++i)
                    archivePath += (rel[i] == '\\') ? '/' : rel[i];
                if (!writer.AddFile(archivePath, full, errorOut))
                {
                    FindClose(h);
                    return false;
                }
            }
        } while (FindNextFileA(h, &find));

        FindClose(h);
        return true;
    }
}

bool PackSaveArchive(const std::vector<ManifestSavePath>& savePaths,
                     const std::string& installDir,
                     const std::string& outZipPath,
                     bool* empty,
                     std::string* errorOut)
{
    if (empty) *empty = true;

    ZipWriter writer;
    if (!writer.Open(outZipPath, errorOut)) return false;

    for (size_t i = 0; i < savePaths.size(); ++i)
    {
        const ManifestSavePath& sp = savePaths[i];
        if (!sp.isFile || sp.isRegex) continue;

        std::string workDir = ExpandEnvAndInstall(sp.workingDirectory, installDir);
        if (workDir.empty()) workDir = installDir;
        else                 workDir = JoinDir(installDir, workDir);
        TrimTrailingSlash(workDir);

        std::string rel = ExpandEnvAndInstall(sp.path, installDir);
        std::string full = JoinDir(workDir, rel);
        std::string archivePrefix = "Files/" + sp.id + "/";

        if (IsDirectory(full))
        {
            std::string subRel = (full.size() > workDir.size())
                ? full.substr(workDir.size() + 1) : std::string();
            if (!AddFolder(writer, workDir, archivePrefix, subRel, errorOut))
            {
                writer.Close(NULL);
                ::remove(outZipPath.c_str());
                return false;
            }
        }
        else if (IsFile(full))
        {
            // Archive-relative path = file path relative to workingDirectory.
            std::string rel2;
            if (full.size() > workDir.size() &&
                (full[workDir.size()] == '\\' || full[workDir.size()] == '/'))
            {
                rel2 = full.substr(workDir.size() + 1);
            }
            else
            {
                // Fall back to bare filename.
                size_t slash = full.find_last_of("\\/");
                rel2 = (slash == std::string::npos) ? full : full.substr(slash + 1);
            }
            std::string archivePath = archivePrefix;
            for (size_t k = 0; k < rel2.size(); ++k)
                archivePath += (rel2[k] == '\\') ? '/' : rel2[k];
            if (!writer.AddFile(archivePath, full, errorOut))
            {
                writer.Close(NULL);
                ::remove(outZipPath.c_str());
                return false;
            }
        }
        // else: nothing to back up for this savepath; not an error.
    }

    if (writer.IsEmpty())
    {
        writer.Close(NULL);
        ::remove(outZipPath.c_str());
        return true;
    }

    if (empty) *empty = false;
    return writer.Close(errorOut);
}

bool UnpackSaveArchive(const std::string& zipPath,
                       const std::vector<ManifestSavePath>& savePaths,
                       const std::string& installDir,
                       std::string* errorOut)
{
    // savePathId -> resolved working directory
    std::map<std::string, std::string> workDirs;
    for (size_t i = 0; i < savePaths.size(); ++i)
    {
        const ManifestSavePath& sp = savePaths[i];
        if (!sp.isFile) continue;
        std::string wd = ExpandEnvAndInstall(sp.workingDirectory, installDir);
        if (wd.empty()) wd = installDir;
        else            wd = JoinDir(installDir, wd);
        TrimTrailingSlash(wd);
        workDirs[sp.id] = wd;
    }

    ZipReader reader;
    if (!reader.Open(zipPath, errorOut)) return false;

    unsigned int count = reader.Count();
    for (unsigned int i = 0; i < count; ++i)
    {
        if (reader.IsDir(i)) continue;
        std::string name;
        if (!reader.EntryName(i, &name)) continue;

        // Expect "Files/<guid>/<rest>"
        if (name.compare(0, 6, "Files/") != 0) continue;
        size_t slash = name.find('/', 6);
        if (slash == std::string::npos) continue;
        std::string id   = name.substr(6, slash - 6);
        std::string rest = name.substr(slash + 1);

        std::map<std::string, std::string>::const_iterator it = workDirs.find(id);
        if (it == workDirs.end()) continue;

        std::string dest = it->second;
        if (!dest.empty()) dest += '\\';
        for (size_t k = 0; k < rest.size(); ++k)
            dest += (rest[k] == '/') ? '\\' : rest[k];

        if (!reader.ExtractTo(i, dest, errorOut))
        {
            reader.Close();
            return false;
        }
    }

    reader.Close();
    return true;
}
