#ifndef LANCOMMANDER_MODELS_ERROR_RESPONSE_H
#define LANCOMMANDER_MODELS_ERROR_RESPONSE_H

#include <string>
#include <vector>

namespace lancommander {

struct ErrorInfo {
    std::string key;
    std::string message;
};

struct ErrorResponse {
    std::string error;
    std::string message;
    std::vector<ErrorInfo> details;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_ERROR_RESPONSE_H
