#ifndef LANCOMMANDER_MODELS_UPDATE_INFO_H
#define LANCOMMANDER_MODELS_UPDATE_INFO_H

#include <string>

namespace lancommander {

struct CheckForUpdateResponse {
    bool update_available = false;
    std::string version;
    std::string download_url;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_UPDATE_INFO_H
