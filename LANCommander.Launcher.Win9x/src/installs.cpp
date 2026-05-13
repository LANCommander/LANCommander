#include "installs.h"

#include <windows.h>
#include <cstdio>

std::string InstallRegistry::FilePath() const
{
    char buf[MAX_PATH];
    DWORD n = GetModuleFileNameA(NULL, buf, MAX_PATH);
    if (n == 0) return std::string("installs.ini");
    std::string path(buf, n);
    size_t slash = path.find_last_of("\\/");
    if (slash == std::string::npos) return std::string("installs.ini");
    return path.substr(0, slash + 1) + "installs.ini";
}

void InstallRegistry::Load()
{
    m_entries.clear();
    FILE* f = fopen(FilePath().c_str(), "rb");
    if (!f) return;
    char line[1024];
    while (fgets(line, sizeof(line), f))
    {
        std::string s(line);
        while (!s.empty() && (s[s.size()-1] == '\n' || s[s.size()-1] == '\r'))
            s.erase(s.size() - 1);
        size_t eq = s.find('=');
        if (eq == std::string::npos) continue;
        std::string key   = s.substr(0, eq);
        std::string value = s.substr(eq + 1);

        Entry e;
        size_t tab = value.find('\t');
        if (tab == std::string::npos)
        {
            e.path = value;
        }
        else
        {
            e.path    = value.substr(0, tab);
            e.version = value.substr(tab + 1);
        }
        m_entries[key] = e;
    }
    fclose(f);
}

void InstallRegistry::Save()
{
    FILE* f = fopen(FilePath().c_str(), "wb");
    if (!f) return;
    for (std::map<std::string, Entry>::const_iterator it = m_entries.begin();
         it != m_entries.end(); ++it)
    {
        if (it->second.version.empty())
            fprintf(f, "%s=%s\r\n", it->first.c_str(), it->second.path.c_str());
        else
            fprintf(f, "%s=%s\t%s\r\n",
                    it->first.c_str(),
                    it->second.path.c_str(),
                    it->second.version.c_str());
    }
    fclose(f);
}

std::string InstallRegistry::Get(const std::string& gameId) const
{
    std::map<std::string, Entry>::const_iterator it = m_entries.find(gameId);
    return it == m_entries.end() ? std::string() : it->second.path;
}

std::string InstallRegistry::GetVersion(const std::string& gameId) const
{
    std::map<std::string, Entry>::const_iterator it = m_entries.find(gameId);
    return it == m_entries.end() ? std::string() : it->second.version;
}

void InstallRegistry::Set(const std::string& gameId, const std::string& installDir)
{
    m_entries[gameId].path = installDir;
}

void InstallRegistry::Set(const std::string& gameId, const std::string& installDir,
                          const std::string& version)
{
    Entry& e = m_entries[gameId];
    e.path    = installDir;
    e.version = version;
}

void InstallRegistry::Remove(const std::string& gameId)
{
    m_entries.erase(gameId);
}
