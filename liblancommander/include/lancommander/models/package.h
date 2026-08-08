#ifndef LANCOMMANDER_MODELS_PACKAGE_H
#define LANCOMMANDER_MODELS_PACKAGE_H

#include <string>

namespace lancommander {

struct Package {
    std::string path;
    std::string version;
    std::string changelog;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_PACKAGE_H
