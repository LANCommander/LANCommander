#ifndef LANCOMMANDER_MODELS_AUTH_H
#define LANCOMMANDER_MODELS_AUTH_H

#include <string>
#include <vector>

namespace lancommander {

struct AuthToken {
    std::string access_token;
    std::string refresh_token;
    std::string expiration;
};

struct AuthenticationProvider {
    std::string name;
    std::string type;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_AUTH_H
