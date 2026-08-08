#ifndef LANCOMMANDER_MODELS_REDISTRIBUTABLE_H
#define LANCOMMANDER_MODELS_REDISTRIBUTABLE_H

#include <string>
#include <vector>

#include "script.h"

namespace lancommander {

struct Redistributable {
    std::string id;
    std::string name;
    std::string description;
    std::vector<Script> scripts;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_REDISTRIBUTABLE_H
