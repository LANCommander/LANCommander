#ifndef LANCOMMANDER_MODELS_PROFILE_H
#define LANCOMMANDER_MODELS_PROFILE_H

#include <string>

namespace lancommander {

struct User {
    std::string id;
    std::string user_name;
    std::string alias;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_PROFILE_H
