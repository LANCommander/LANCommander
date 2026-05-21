#ifndef LANCOMMANDER_MODELS_LOBBY_H
#define LANCOMMANDER_MODELS_LOBBY_H

#include <string>

namespace lancommander {

struct Lobby {
    std::string id;
    std::string game_id;
    std::string external_game_id;
    std::string external_username;
    std::string external_user_id;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_LOBBY_H
