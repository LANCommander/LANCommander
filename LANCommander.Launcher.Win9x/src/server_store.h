#ifndef LANCOMMANDER_WIN9X_SERVER_STORE_H
#define LANCOMMANDER_WIN9X_SERVER_STORE_H

#include <string>
#include <vector>

struct ServerBookmark
{
    std::string name;          // optional display name; defaults to URL
    std::string url;
    std::string userName;
    std::string accessToken;
    std::string refreshToken;
    std::string expiration;
};

// Persists multiple server entries as JSON in servers.json next to the exe.
// Each bookmark carries its own credentials so users can switch between LAN
// events without re-typing passwords.
class ServerStore
{
public:
    void Load();
    void Save();

    const std::vector<ServerBookmark>& Entries() const { return m_entries; }

    // Find by URL match. Returns NULL if absent.
    const ServerBookmark* Find(const std::string& url) const;

    // Insert or update by URL. Returns the index of the upserted entry.
    size_t Upsert(const ServerBookmark& entry);

    void Remove(const std::string& url);
    void Clear();

private:
    std::string FilePath() const;
    std::vector<ServerBookmark> m_entries;
};

#endif
