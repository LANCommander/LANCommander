#ifndef LANCOMMANDER_WIN9X_GAME_CLIENT_H
#define LANCOMMANDER_WIN9X_GAME_CLIENT_H

#include <map>
#include <string>
#include <vector>

#include "http_client.h"

struct GameSummary
{
    std::string id;
    std::string title;
    std::string sortTitle;
    int         releasedYear;
    std::string coverMediaId;
    std::string coverCrc32;

    std::string description;
    std::string developers;
    std::string publishers;
    std::string genres;
    bool        inLibrary;

    GameSummary() : releasedYear(0), inLibrary(false) {}
};

struct ManifestAction
{
    std::string name;
    std::string path;
    std::string arguments;
    std::string workingDirectory;
    bool        isPrimary;
    int         sortOrder;
    std::map<std::string, std::string> variables;

    ManifestAction() : isPrimary(false), sortOrder(0) {}
};

struct ManifestSavePath
{
    std::string id;
    std::string path;
    std::string workingDirectory;
    bool        isFile;
    bool        isRegex;

    ManifestSavePath() : isFile(true), isRegex(false) {}
};

struct GameManifest
{
    std::string id;
    std::string title;
    std::string version;
    std::vector<ManifestAction>   actions;
    std::vector<ManifestSavePath> savePaths;
};

// Summary of a redistributable attached to a game. Fetched as part of
// /api/Games/{id} (the Game.Redistributables nav property in the SDK).
struct RedistributableSummary
{
    std::string id;
    std::string name;
    std::string description;
};

// Parses /api/Games/{id}/Manifest JSON. Exposed so cached manifests can be
// rehydrated without going through the network path.
bool ParseManifestJson(const std::string& json, GameManifest* out,
                       std::string* errorOut);

// Mirrors LANCommander.SDK.Clients.GameClient. Owns endpoints rooted at
// /api/Games. Stateless apart from the shared HttpClient.
class GameClient
{
public:
    explicit GameClient(HttpClient& http);

    bool GetAll(std::vector<GameSummary>* gamesOut, std::string* errorOut);

    bool DownloadGame(const std::string& gameId, const std::string& destPath,
                      DownloadProgressFn progress, void* userData,
                      std::string* errorOut);

    bool GetManifest(const std::string& gameId, GameManifest* out,
                     std::string* errorOut);
    bool FetchManifestJson(const std::string& gameId, std::string* jsonOut,
                           std::string* errorOut);

    bool CheckForUpdate(const std::string& gameId,
                        const std::string& installedVersion,
                        bool* updateAvailableOut, std::string* errorOut);

    void NotifyStarted(const std::string& gameId);
    void NotifyStopped(const std::string& gameId);

    bool GetAddons(const std::string& gameId,
                   std::vector<GameSummary>* out, std::string* errorOut);

    // Returns the redistributables attached to this game (DirectX, VC runtimes,
    // etc.). The Win9x launcher uses this to drive a manual "Install
    // prerequisites" picker — there's no scripted detect step.
    bool GetRedistributables(const std::string& gameId,
                             std::vector<RedistributableSummary>* out,
                             std::string* errorOut);

private:
    HttpClient& m_http;
};

#endif
