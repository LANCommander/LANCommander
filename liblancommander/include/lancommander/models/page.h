#ifndef LANCOMMANDER_MODELS_PAGE_H
#define LANCOMMANDER_MODELS_PAGE_H

#include <string>

namespace lancommander {

struct Page {
    std::string id;
    std::string title;
    std::string slug;
    std::string route;
    std::string contents;
    int sort_order = 0;
    std::string parent_id;
    std::string created_on;
    std::string updated_on;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_PAGE_H
