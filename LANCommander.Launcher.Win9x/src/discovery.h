#ifndef LANCOMMANDER_WIN9X_DISCOVERY_H
#define LANCOMMANDER_WIN9X_DISCOVERY_H

#include <string>
#include <vector>

struct DiscoveredServer
{
    std::string address; // server-reported URL (preferred)
    std::string name;
    std::string version;
    std::string remoteIp; // sender IP, used as a fallback when address is empty
};

// One-shot Winsock init/teardown. Safe to call repeatedly.
bool DiscoveryStartup();
void DiscoveryShutdown();

// Broadcasts a probe and collects replies until `timeoutMs` elapses.
// `out` is appended to; duplicates (by address+remoteIp) are de-duplicated.
bool DiscoverServers(unsigned int timeoutMs,
                     std::vector<DiscoveredServer>* out,
                     std::string* errorOut);

#endif
