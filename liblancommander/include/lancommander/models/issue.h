#ifndef LANCOMMANDER_MODELS_ISSUE_H
#define LANCOMMANDER_MODELS_ISSUE_H

#include <string>

namespace lancommander {

struct Issue {
    std::string description;
    std::string game_id;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_ISSUE_H
