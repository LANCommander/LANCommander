#ifndef LANCOMMANDER_WIN9X_INSTALLS_H
#define LANCOMMANDER_WIN9X_INSTALLS_H

#include <map>
#include <string>

// Flat INI-ish file next to the exe mapping
//   game-guid = install-dir [TAB version]
// where the version part is optional (entries written by older builds have
// only the dir).
class InstallRegistry
{
public:
    void Load();
    void Save();

    std::string Get(const std::string& gameId) const;        // install dir, "" if absent
    std::string GetVersion(const std::string& gameId) const; // "" if absent or no version
    void Set(const std::string& gameId, const std::string& installDir);
    void Set(const std::string& gameId, const std::string& installDir,
             const std::string& version);
    void Remove(const std::string& gameId);

private:
    struct Entry { std::string path; std::string version; };
    std::string FilePath() const;
    std::map<std::string, Entry> m_entries;
};

#endif
