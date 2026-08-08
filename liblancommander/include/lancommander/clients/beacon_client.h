#ifndef LANCOMMANDER_CLIENTS_BEACON_CLIENT_H
#define LANCOMMANDER_CLIENTS_BEACON_CLIENT_H

#include <string>
#include <vector>

#include "../models/server.h"
#include "../types.h"

namespace lancommander {

class BeaconClient {
public:
    static constexpr int DEFAULT_PORT = 35891;

    // Broadcasts a UDP probe and collects server replies until timeout_ms elapses.
    // Results are appended to `out`; duplicates are de-duplicated by address.
    Result<std::vector<DiscoveredServer>> discover(unsigned int timeout_ms,
                                                   int port = DEFAULT_PORT);
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_BEACON_CLIENT_H
