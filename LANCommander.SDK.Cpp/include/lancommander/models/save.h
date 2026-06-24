#ifndef LANCOMMANDER_MODELS_SAVE_H
#define LANCOMMANDER_MODELS_SAVE_H

#include <string>

namespace lancommander {

struct GameSave {
    std::string id;
    std::string game_id;
    std::string created_on;
    std::string updated_on;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_SAVE_H
