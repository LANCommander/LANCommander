#ifndef LANCOMMANDER_MODELS_PLAY_SESSION_H
#define LANCOMMANDER_MODELS_PLAY_SESSION_H

#include <string>

namespace lancommander {

struct PlaySession {
    std::string id;
    std::string start;
    std::string end;
    std::string game_id;
    std::string user_id;
    std::string created_on;
    std::string updated_on;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_PLAY_SESSION_H
