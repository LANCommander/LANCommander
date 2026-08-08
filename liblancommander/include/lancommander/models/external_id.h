#ifndef LANCOMMANDER_MODELS_EXTERNAL_ID_H
#define LANCOMMANDER_MODELS_EXTERNAL_ID_H

#include <string>

namespace lancommander {

struct GameExternalId {
    std::string id;
    std::string provider;
    std::string external_id;
    std::string created_on;
    std::string updated_on;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_EXTERNAL_ID_H
