#ifndef LANCOMMANDER_MODELS_DEPOT_H
#define LANCOMMANDER_MODELS_DEPOT_H

#include <string>
#include <vector>

#include "collection.h"
#include "company.h"
#include "engine.h"
#include "genre.h"
#include "media.h"
#include "multiplayer_mode.h"
#include "platform.h"
#include "tag.h"
#include "game.h"

namespace lancommander {

struct DepotGame {
    std::string id;
    std::string title;
    std::string sort_title;
    std::string directory_name;
    std::string notes;
    std::string description;
    bool singleplayer = false;
    std::string created_on;
    std::string released_on;
    bool in_library = false;
    GameType type = GameType::MainGame;
    Media cover;
    std::vector<Collection> collections;
    std::vector<Company> developers;
    std::vector<Company> publishers;
    std::string engine_id;
    std::vector<Genre> genres;
    std::vector<MultiplayerMode> multiplayer_modes;
    std::vector<Platform> platforms;
    std::vector<Tag> tags;
};

struct DepotResults {
    std::vector<DepotGame> games;
    std::vector<Collection> collections;
    std::vector<Company> companies;
    std::vector<Engine> engines;
    std::vector<Genre> genres;
    std::vector<Platform> platforms;
    std::vector<Tag> tags;
    std::vector<std::string> popular;
    std::vector<std::string> backlog;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_DEPOT_H
