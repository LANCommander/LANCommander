#ifndef LANCOMMANDER_MODELS_TOOL_H
#define LANCOMMANDER_MODELS_TOOL_H

#include <string>
#include <vector>

#include "archive.h"
#include "script.h"

namespace lancommander {

struct Tool {
    std::string id;
    std::string name;
    std::string description;
    std::string notes;
    std::string released_on;
    std::string created_on;
    std::string updated_on;
    std::vector<Archive> archives;
    std::vector<Script> scripts;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_TOOL_H
