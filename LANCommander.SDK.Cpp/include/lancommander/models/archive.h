#ifndef LANCOMMANDER_MODELS_ARCHIVE_H
#define LANCOMMANDER_MODELS_ARCHIVE_H

#include <string>

namespace lancommander {

struct Archive {
    std::string id;
    std::string changelog;
    std::string object_key;
    std::string version;
    long long compressed_size = 0;
    long long uncompressed_size = 0;
    std::string created_on;
    std::string updated_on;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_ARCHIVE_H
