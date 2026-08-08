#ifndef LANCOMMANDER_MODELS_SERVER_H
#define LANCOMMANDER_MODELS_SERVER_H

#include <string>

namespace lancommander {

struct DiscoveredServer {
    std::string address;
    std::string name;
    std::string version;
    std::string remote_ip;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_SERVER_H
